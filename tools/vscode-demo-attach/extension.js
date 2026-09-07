// Caesarea Demo Attach: lets the demo control UI ask VS Code to attach the .NET debugger to a
// running demo service, or to let go of it again, via URIs such as:
//   vscode://caesarea-demo.demo-attach/attach?processName=OperationsAgent.Api.exe
//   vscode://caesarea-demo.demo-attach/detach?processName=OperationsAgent.Api.exe
const vscode = require('vscode');
const childProcess = require('child_process');

// Only plain executable names are accepted, so the URI can never smuggle shell syntax into tasklist.
const processNamePattern = /^[A-Za-z0-9._-]+$/;

// Every debug session seen since activation, by id. VS Code exposes only the active session, and
// the presenter attaches to several services, so the extension keeps its own list to detach from
// the right one.
const sessions = new Map();

function readProcessName(uri) {
    const processName = new URLSearchParams(uri.query).get('processName');
    if (!processName || !processNamePattern.test(processName)) {
        vscode.window.showErrorMessage('Caesarea Demo Attach: a valid processName query parameter is required.');
        return undefined;
    }
    return processName;
}

// A session belongs to a service when this extension started it (named after the process) or
// when a launch.json attach configuration named the same process.
function isSessionFor(session, processName) {
    return !!session
        && (session.name === `Attach: ${processName}` || session.configuration?.processName === processName);
}

function findSession(processName) {
    for (const session of sessions.values()) {
        if (isSessionFor(session, processName)) {
            return session;
        }
    }
    const active = vscode.debug.activeDebugSession;
    return isSessionFor(active, processName) ? active : undefined;
}

function findProcessId(processName) {
    const output = childProcess
        .execSync(`tasklist /FI "IMAGENAME eq ${processName}" /FO CSV /NH`, { encoding: 'utf8' });
    const line = output.split(/\r?\n/).find((candidate) => candidate.startsWith('"'));
    if (!line) {
        return undefined;
    }
    const columns = line.split('","');
    return columns.length > 1 ? Number(columns[1].replace(/"/g, '')) : undefined;
}

async function handleAttach(uri) {
    const processName = readProcessName(uri);
    if (!processName) {
        return;
    }

    if (findSession(processName)) {
        vscode.window.showInformationMessage(`Caesarea Demo Attach: already attached to ${processName}.`);
        return;
    }

    const processId = findProcessId(processName);
    if (!processId) {
        vscode.window.showErrorMessage(`Caesarea Demo Attach: no running process named ${processName} was found.`);
        return;
    }

    const folder = vscode.workspace.workspaceFolders?.[0];
    const started = await vscode.debug.startDebugging(folder, {
        name: `Attach: ${processName}`,
        type: 'coreclr',
        request: 'attach',
        processId: String(processId)
    });

    if (started) {
        vscode.window.showInformationMessage(`Caesarea Demo Attach: attached to ${processName} (PID ${processId}).`);
    } else {
        vscode.window.showErrorMessage(`Caesarea Demo Attach: could not attach to ${processName}.`);
    }
}

async function handleDetach(uri) {
    const processName = readProcessName(uri);
    if (!processName) {
        return;
    }

    const session = findSession(processName);
    if (!session) {
        vscode.window.showWarningMessage(
            `Caesarea Demo Attach: no debug session for ${processName} is known to this extension. Use Disconnect in the debug toolbar.`);
        return;
    }

    // Stopping an attach session disconnects: the debugger lets go and the service keeps running.
    await vscode.debug.stopDebugging(session);
    vscode.window.showInformationMessage(`Caesarea Demo Attach: detached from ${processName}.`);
}

function activate(context) {
    context.subscriptions.push(
        vscode.debug.onDidStartDebugSession((session) => sessions.set(session.id, session)),
        vscode.debug.onDidTerminateDebugSession((session) => sessions.delete(session.id)),
        vscode.window.registerUriHandler({
            handleUri: async (uri) => {
                if (uri.path === '/attach') {
                    await handleAttach(uri);
                } else if (uri.path === '/detach') {
                    await handleDetach(uri);
                }
            }
        }));
}

module.exports = { activate };
