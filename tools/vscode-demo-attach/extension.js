// Caesarea Demo Attach: lets the demo control UI ask VS Code to attach the .NET debugger to a
// running demo service, or to let go of it again, via URIs such as:
//   vscode://caesarea-demo.demo-attach/attach?processName=OperationsAgent.Api.exe&processId=4242
//   vscode://caesarea-demo.demo-attach/detach?processName=OperationsAgent.Api.exe&processId=4242
// processId is what the service itself reported and is used as given; processName names the debug
// session and is the lookup key when no id came along (an older service, or a hand-typed URI).
const vscode = require('vscode');
const childProcess = require('child_process');
const path = require('path');

// Only plain executable names are accepted, so the URI can never smuggle shell syntax into a
// process listing.
const processNamePattern = /^[A-Za-z0-9._-]+$/;
const processIdPattern = /^\d+$/;

// Every debug session seen since activation, by id. VS Code exposes only the active session, and
// the presenter attaches to several services, so the extension keeps its own list to detach from
// the right one.
const sessions = new Map();

// The process id each coreclr session actually debugs, by session id, taken from the debug
// adapter's own "process" event. A launch.json attach by name resolves its process only when it
// starts and its configuration never says which; this is the only place the answer exists.
const sessionProcessIds = new Map();

// "Caesarea Demo Attach" in the Output panel, and on disk under the window's extension host logs:
// what was asked, what the debugger answered, and what it said while failing - the facts a
// presenter needs when a button on the switchboard did nothing.
let log;

function readTarget(uri) {
    const query = new URLSearchParams(uri.query);
    const processName = query.get('processName');
    if (!processName || !processNamePattern.test(processName)) {
        log?.error(`rejected ${uri.path}: processName missing or invalid`);
        vscode.window.showErrorMessage('Caesarea Demo Attach: a valid processName query parameter is required.');
        return undefined;
    }
    const processIdText = query.get('processId');
    const processId = processIdText && processIdPattern.test(processIdText) ? Number(processIdText) : undefined;
    return { processName, processId };
}

function toProcessId(value) {
    return value !== undefined && value !== null && processIdPattern.test(String(value)) ? Number(value) : undefined;
}

// The process id a session debugs: what the adapter reported, else what the configuration asked
// for, else unknown (a launch.json attach by name whose process event has not been seen).
function processIdOf(session, reportedProcessIds) {
    return reportedProcessIds.get(session.id) ?? toProcessId(session.configuration?.processId);
}

function nameMatches(session, processName) {
    const configuration = session.configuration ?? {};
    return configuration.processName === processName || configuration.caesareaProcessName === processName;
}

// Which session a request is about. Only attach sessions qualify: stopping a launch session
// (F5 on a service) would terminate the process it started, and this extension only ever lets
// go. With an id in the request, a session known to debug exactly that id wins; a session known
// to debug another id is never it; only a session whose id is unknown may still be matched by
// name, and only when it is the sole such session - guessing between two would detach the wrong
// one. Without an id, the name decides, as before ids were sent.
function chooseSession(candidates, target, reportedProcessIds = new Map()) {
    const known = candidates.filter((session) => !!session && session.configuration?.request === 'attach');
    if (target.processId !== undefined) {
        const exact = known.find((session) => processIdOf(session, reportedProcessIds) === target.processId);
        if (exact) {
            return exact;
        }
        const unknownByName = known.filter((session) =>
            processIdOf(session, reportedProcessIds) === undefined && nameMatches(session, target.processName));
        return unknownByName.length === 1 ? unknownByName[0] : undefined;
    }
    return known.find((session) => nameMatches(session, target.processName));
}

function findSession(target) {
    const candidates = [...sessions.values()];
    const active = vscode.debug.activeDebugSession;
    if (active && !sessions.has(active.id)) {
        candidates.push(active);
    }
    return chooseSession(candidates, target, sessionProcessIds);
}

// Windows lists processes with tasklist; macOS and Linux with ps. On Unix the demo service runs
// either as its apphost, named after the service, or as "dotnet <Service>.dll", so both shapes are
// matched. Linux truncates the kernel process name to 15 characters, which is why the full
// argument list is read rather than the bare command name.
function findProcessId(processName) {
    if (process.platform === 'win32') {
        const output = childProcess
            .execFileSync('tasklist', ['/FI', `IMAGENAME eq ${processName}`, '/FO', 'CSV', '/NH'], { encoding: 'utf8' });
        const line = output.split(/\r?\n/).find((candidate) => candidate.startsWith('"'));
        if (!line) {
            return undefined;
        }
        const columns = line.split('","');
        return columns.length > 1 ? Number(columns[1].replace(/"/g, '')) : undefined;
    }

    const output = childProcess.execFileSync('ps', ['-A', '-o', 'pid=,args='], { encoding: 'utf8' });
    for (const line of output.split('\n')) {
        const match = line.trim().match(/^(\d+)\s+(\S+)(?:\s+(\S+))?/);
        if (!match) {
            continue;
        }
        const executable = path.basename(match[2]);
        const firstArgument = match[3] ? path.basename(match[3]) : '';
        if (executable === processName || firstArgument === `${processName}.dll`) {
            return Number(match[1]);
        }
    }
    return undefined;
}

async function handleAttach(uri) {
    const target = readTarget(uri);
    if (!target) {
        return;
    }
    log?.info(`attach requested: ${target.processName}${target.processId === undefined ? '' : ` (PID ${target.processId})`}`);

    const processId = target.processId ?? findProcessId(target.processName);
    if (!processId) {
        log?.error(`no running process named ${target.processName}`);
        vscode.window.showErrorMessage(`Caesarea Demo Attach: no running process named ${target.processName} was found.`);
        return;
    }

    const existing = findSession({ processName: target.processName, processId });
    if (existing) {
        log?.info(`already attached: session "${existing.name}" debugs PID ${processId}`);
        vscode.window.showInformationMessage(`Caesarea Demo Attach: already attached to ${target.processName} (PID ${processId}).`);
        return;
    }

    const folder = vscode.workspace.workspaceFolders?.[0];
    log?.info(`starting coreclr attach to PID ${processId}${folder ? ` in ${folder.name}` : ' without a workspace folder'}`);
    // caesareaProcessName rides along in the configuration so a later request without an id
    // still finds this session by name.
    const started = await vscode.debug.startDebugging(folder, {
        name: `Attach: ${target.processName} (${processId})`,
        type: 'coreclr',
        request: 'attach',
        processId: String(processId),
        caesareaProcessName: target.processName
    });

    if (started) {
        log?.info(`attached to ${target.processName} (PID ${processId})`);
        vscode.window.showInformationMessage(`Caesarea Demo Attach: attached to ${target.processName} (PID ${processId}).`);
    } else {
        log?.error(`VS Code declined the attach to ${target.processName} (PID ${processId}); see the debugger's own message above, if any`);
        vscode.window.showErrorMessage(`Caesarea Demo Attach: could not attach to ${target.processName} (PID ${processId}). See the "Caesarea Demo Attach" output for the debugger's reason.`);
    }
}

async function handleDetach(uri) {
    const target = readTarget(uri);
    if (!target) {
        return;
    }
    log?.info(`detach requested: ${target.processName}${target.processId === undefined ? '' : ` (PID ${target.processId})`}`);

    const session = findSession(target);
    if (!session) {
        const described = target.processId === undefined ? target.processName : `${target.processName} (PID ${target.processId})`;
        log?.warn(`no debug session for ${described} among ${sessions.size} known session(s)`);
        vscode.window.showWarningMessage(
            `Caesarea Demo Attach: no debug session for ${described} is known to this extension. Use Disconnect in the debug toolbar.`);
        return;
    }

    // Stopping an attach session disconnects: the debugger lets go and the service keeps running.
    log?.info(`stopping session "${session.name}"`);
    await vscode.debug.stopDebugging(session);
    log?.info(`detached from ${target.processName}`);
    vscode.window.showInformationMessage(`Caesarea Demo Attach: detached from ${target.processName}.`);
}

function activate(context) {
    log = vscode.window.createOutputChannel('Caesarea Demo Attach', { log: true });
    log.info(`activated on ${process.platform}`);
    context.subscriptions.push(
        log,
        vscode.debug.onDidStartDebugSession((session) => {
            sessions.set(session.id, session);
            log.info(`session started: "${session.name}" (${session.type})`);
        }),
        vscode.debug.onDidTerminateDebugSession((session) => {
            sessions.delete(session.id);
            sessionProcessIds.delete(session.id);
            log.info(`session ended: "${session.name}"`);
        }),
        // The adapter announces the debuggee's system process id in its "process" event; that is
        // the one fact a launch.json attach by name never writes down anywhere else. Its output
        // and errors are the reason an attach failed, so they are kept too.
        vscode.debug.registerDebugAdapterTrackerFactory('coreclr', {
            createDebugAdapterTracker(session) {
                return {
                    onDidSendMessage(message) {
                        if (message?.type !== 'event') {
                            return;
                        }
                        if (message.event === 'process') {
                            const processId = toProcessId(message.body?.systemProcessId);
                            if (processId !== undefined) {
                                sessionProcessIds.set(session.id, processId);
                                log.info(`session "${session.name}" debugs PID ${processId}`);
                            }
                        } else if (message.event === 'output' && message.body?.category !== 'stdout' && message.body?.output) {
                            log.info(`debugger: ${String(message.body.output).trim()}`);
                        }
                    },
                    onError(error) {
                        log.error(`debug adapter error for "${session.name}": ${error?.message ?? error}`);
                    },
                    onExit(code, signal) {
                        if (code || signal) {
                            log.warn(`debug adapter for "${session.name}" exited with code ${code}${signal ? ` (${signal})` : ''}`);
                        }
                    }
                };
            }
        }),
        vscode.window.registerUriHandler({
            handleUri: async (uri) => {
                if (uri.path === '/attach') {
                    await handleAttach(uri);
                } else if (uri.path === '/detach') {
                    await handleDetach(uri);
                } else {
                    log.warn(`unknown route ${uri.path}`);
                }
            }
        }));
}

module.exports = { activate, chooseSession };
