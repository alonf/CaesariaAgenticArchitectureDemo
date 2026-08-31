// Caesarea Demo Attach: lets the demo control UI ask VS Code to attach the .NET debugger to a
// running demo service, via a URI such as:
//   vscode://caesarea-demo.demo-attach/attach?processName=OperationsAgent.Api.exe
const vscode = require('vscode');
const childProcess = require('child_process');

// Only plain executable names are accepted, so the URI can never smuggle shell syntax into tasklist.
const processNamePattern = /^[A-Za-z0-9._-]+$/;

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
    const processName = new URLSearchParams(uri.query).get('processName');
    if (!processName || !processNamePattern.test(processName)) {
        vscode.window.showErrorMessage('Caesarea Demo Attach: a valid processName query parameter is required.');
        return;
    }

    const alreadyAttached = vscode.debug.activeDebugSession?.name === `Attach: ${processName}`;
    if (alreadyAttached) {
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

function activate(context) {
    context.subscriptions.push(vscode.window.registerUriHandler({
        handleUri: async (uri) => {
            if (uri.path === '/attach') {
                await handleAttach(uri);
            }
        }
    }));
}

module.exports = { activate };
