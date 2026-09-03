var builder = DistributedApplication.CreateBuilder(args);

var smartpoleSimulator = builder.AddProject<Projects.SmartPole_Simulator_Api>("smartpole-simulator-api");

var energyHub = builder.AddProject<Projects.EnergyHub_Api>("energyhub-api")
    .WithReference(smartpoleSimulator)
    .WaitFor(smartpoleSimulator);

var commandCenterApi = builder.AddProject<Projects.CommandCenter_Api>("commandcenter-api")
    .WithReference(energyHub)
    .WaitFor(energyHub);

// The Security domain: an authoritative hub whose records only its own agent may read, and the
// agent that reads them. The Operations Agent deliberately has no reference to the hub.
var securityHub = builder.AddProject<Projects.SecurityHub_Api>("securityhub-api");

var securityAgentApi = builder.AddProject<Projects.SecurityAgent_Api>("securityagent-api")
    .WithReference(securityHub)
    .WaitFor(securityHub);

// The workforce domain: the work-order system of record, and the only agent permitted to read it.
// The Operations Agent has no reference to the hub - it consults the agent over A2A instead.
var workforceHub = builder.AddProject<Projects.WorkforceHub_Api>("workforcehub-api");

var workforceAgentApi = builder.AddProject<Projects.WorkforceAgent_Api>("workforceagent-api")
    .WithReference(workforceHub)
    .WaitFor(workforceHub);

var operationsAgentApi = builder.AddProject<Projects.OperationsAgent_Api>("operationsagent-api")
    .WithReference(energyHub)
    .WithReference(commandCenterApi)
    .WithReference(securityAgentApi)
    .WithReference(workforceAgentApi)
    .WaitFor(energyHub);

// DemoScenario references the Operations Agent (to propagate stage changes) but does not wait for
// it, so an agent startup problem never blocks the deterministic Stage 0 lecture path.
var demoScenarioApi = builder.AddProject<Projects.DemoScenario_Api>("demoscenario-api")
    .WithReference(smartpoleSimulator)
    .WithReference(energyHub)
    .WithReference(commandCenterApi)
    .WithReference(operationsAgentApi)
    .WithReference(securityHub)
    .WithReference(workforceHub)
    .WaitFor(smartpoleSimulator)
    .WaitFor(energyHub)
    .WaitFor(commandCenterApi)
    .WaitFor(securityHub);

// The web apps reference the Operations Agent but deliberately do not wait for it: a Foundry/agent
// startup problem must never block the deterministic Stage 0 lecture path (Section 32 fallbacks).
builder.AddProject<Projects.CommandCenter_Web>("commandcenter-web")
    .WithReference(commandCenterApi)
    .WithReference(operationsAgentApi)
    .WaitFor(commandCenterApi)
    .WithExternalHttpEndpoints();

// The switchboard also addresses the Energy Hub and the two peer agents directly, because demo
// snippets live in the service whose code they pause and each is armed at its own boundary. Its
// reference to the Workforce Hub is different in kind: it reads the work orders in full, which is
// the presenter's own view of what the domain withheld, and is why that route is loopback only.
builder.AddProject<Projects.DemoControl_Web>("democontrol-web")
    .WithReference(demoScenarioApi)
    .WithReference(operationsAgentApi)
    .WithReference(energyHub)
    .WithReference(securityAgentApi)
    .WithReference(workforceAgentApi)
    .WithReference(workforceHub)
    .WaitFor(demoScenarioApi)
    .WithExternalHttpEndpoints();

await builder.Build().RunAsync();
