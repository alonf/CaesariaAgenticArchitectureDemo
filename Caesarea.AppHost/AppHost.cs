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

var operationsAgentApi = builder.AddProject<Projects.OperationsAgent_Api>("operationsagent-api")
    .WithReference(energyHub)
    .WithReference(commandCenterApi)
    .WithReference(securityAgentApi)
    .WaitFor(energyHub);

// DemoScenario references the Operations Agent (to propagate stage changes) but does not wait for
// it, so an agent startup problem never blocks the deterministic Stage 0 lecture path.
var demoScenarioApi = builder.AddProject<Projects.DemoScenario_Api>("demoscenario-api")
    .WithReference(smartpoleSimulator)
    .WithReference(energyHub)
    .WithReference(commandCenterApi)
    .WithReference(operationsAgentApi)
    .WithReference(securityHub)
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

// The switchboard also addresses the Energy Hub and the Security Agent directly, because demo
// snippets live in the service whose code they pause and each is armed at its own boundary.
builder.AddProject<Projects.DemoControl_Web>("democontrol-web")
    .WithReference(demoScenarioApi)
    .WithReference(operationsAgentApi)
    .WithReference(energyHub)
    .WithReference(securityAgentApi)
    .WaitFor(demoScenarioApi)
    .WithExternalHttpEndpoints();

await builder.Build().RunAsync();
