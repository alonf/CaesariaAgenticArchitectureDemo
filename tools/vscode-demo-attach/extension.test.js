// Run with: node --test tools/vscode-demo-attach/extension.test.js
// Covers the one decision the extension makes on its own: which debug session a request is
// about. Everything else is a VS Code API call.
const test = require('node:test');
const assert = require('node:assert/strict');
const Module = require('node:module');

// extension.js requires the vscode module, which exists only inside VS Code; a stub stands in
// for the duration of the require.
const originalLoad = Module._load;
Module._load = function (request, ...rest) {
    if (request === 'vscode') {
        return { window: {}, debug: {}, workspace: {} };
    }
    return originalLoad.call(this, request, ...rest);
};
const { chooseSession } = require('./extension.js');
Module._load = originalLoad;

let nextId = 1;
const session = (configuration) => ({ id: `session-${nextId++}`, configuration });
const attachedTo = (processId, processName) => session({ processId: String(processId), caesareaProcessName: processName });
const byName = (processName) => session({ processName });
const reported = (...pairs) => new Map(pairs.map(([s, pid]) => [s.id, pid]));

test('a request with a process id matches only the session on that id', () => {
    // Two checkouts can run a service of the same name; the id is what tells them apart, so a
    // request for PID 222 must neither report "already attached" nor detach PID 111's session.
    const first = attachedTo(111, 'OperationsAgent.Api.exe');
    assert.equal(chooseSession([first], { processName: 'OperationsAgent.Api.exe', processId: 111 }), first);
    assert.equal(chooseSession([first], { processName: 'OperationsAgent.Api.exe', processId: 222 }), undefined);
});

test('an exact id match wins over a name-only session, whatever the order', () => {
    // A launch.json attach by name and an extension session on the requested id: the request
    // for PID 222 is about the latter, even when the name-only session is listed first.
    const named = byName('OperationsAgent.Api.exe');
    const exact = attachedTo(222, 'OperationsAgent.Api.exe');
    assert.equal(chooseSession([named, exact], { processName: 'OperationsAgent.Api.exe', processId: 222 }), exact);
    assert.equal(chooseSession([exact, named], { processName: 'OperationsAgent.Api.exe', processId: 222 }), exact);
});

test('a name-only session is matched by name only while its process id is unknown', () => {
    // Once the adapter has reported which process the named session debugs, that id decides.
    const named = byName('OperationsAgent.Api.exe');
    assert.equal(chooseSession([named], { processName: 'OperationsAgent.Api.exe', processId: 222 }), named);
    assert.equal(chooseSession([named], { processName: 'OperationsAgent.Api.exe', processId: 222 }, reported([named, 111])), undefined);
    assert.equal(chooseSession([named], { processName: 'OperationsAgent.Api.exe', processId: 111 }, reported([named, 111])), named);
});

test('two name-only sessions of unknown id are ambiguous for an id request', () => {
    // Guessing between them would detach the wrong one; the request is refused instead.
    const one = byName('OperationsAgent.Api.exe');
    const two = byName('OperationsAgent.Api.exe');
    assert.equal(chooseSession([one, two], { processName: 'OperationsAgent.Api.exe', processId: 222 }), undefined);
});

test('a request without a process id falls back to the name', () => {
    assert.equal(chooseSession([attachedTo(111, 'OperationsAgent.Api.exe')], { processName: 'OperationsAgent.Api.exe' }) !== undefined, true);
    assert.equal(chooseSession([attachedTo(111, 'EnergyHub.Api.exe')], { processName: 'OperationsAgent.Api.exe' }), undefined);
    assert.equal(chooseSession([byName('OperationsAgent.Api.exe')], { processName: 'OperationsAgent.Api.exe' }) !== undefined, true);
});

test('absent sessions never match', () => {
    assert.equal(chooseSession([undefined, null], { processName: 'OperationsAgent.Api.exe', processId: 1 }), undefined);
    assert.equal(chooseSession([session(undefined)], { processName: 'OperationsAgent.Api.exe' }), undefined);
});
