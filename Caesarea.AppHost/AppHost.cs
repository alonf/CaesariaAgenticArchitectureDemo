var builder = DistributedApplication.CreateBuilder(args);

var smartpoleSimulator = builder.AddProject<Projects.SmartPole_Simulator_Api>("smartpole-simulator-api");

var energyHub = builder.AddProject<Projects.EnergyHub_Api>("energyhub-api")
    .WithReference(smartpoleSimulator)
    .WaitFor(smartpoleSimulator);

var commandCenterApi = builder.AddProject<Projects.CommandCenter_Api>("commandcenter-api")
    .WithReference(energyHub)
    .WaitFor(energyHub);

var operationsAgentApi = builder.AddProject<Projects.OperationsAgent_Api>("operationsagent-api")
    .WithReference(energyHub)
    .WithReference(commandCenterApi)
    .WaitFor(energyHub);

// DemoScenario references the Operations Agent (to propagate stage changes) but does not wait for
// it, so an agent startup problem never blocks the deterministic Stage 0 lecture path.
var demoScenarioApi = builder.AddProject<Projects.DemoScenario_Api>("demoscenario-api")
    .WithReference(smartpoleSimulator)
    .WithReference(energyHub)
    .WithReference(commandCenterApi)
    .WithReference(operationsAgentApi)
    .WaitFor(smartpoleSimulator)
    .WaitFor(energyHub)
    .WaitFor(commandCenterApi);

// The web apps reference the Operations Agent but deliberately do not wait for it: a Foundry/agent
// startup problem must never block the deterministic Stage 0 lecture path (Section 32 fallbacks).
builder.AddProject<Projects.CommandCenter_Web>("commandcenter-web")
    .WithReference(commandCenterApi)
    .WithReference(operationsAgentApi)
    .WaitFor(commandCenterApi)
    .WithExternalHttpEndpoints();

builder.AddProject<Projects.DemoControl_Web>("democontrol-web")
    .WithReference(demoScenarioApi)
    .WithReference(operationsAgentApi)
    .WaitFor(demoScenarioApi)
    .WithExternalHttpEndpoints();

await builder.Build().RunAsync();
