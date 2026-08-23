var builder = DistributedApplication.CreateBuilder(args);

var smartpoleSimulator = builder.AddProject<Projects.SmartPole_Simulator_Api>("smartpole-simulator-api");

var energyHub = builder.AddProject<Projects.EnergyHub_Api>("energyhub-api")
    .WithReference(smartpoleSimulator)
    .WaitFor(smartpoleSimulator);

var commandCenterApi = builder.AddProject<Projects.CommandCenter_Api>("commandcenter-api")
    .WithReference(energyHub)
    .WaitFor(energyHub);

var demoScenarioApi = builder.AddProject<Projects.DemoScenario_Api>("demoscenario-api")
    .WithReference(smartpoleSimulator)
    .WithReference(energyHub)
    .WithReference(commandCenterApi)
    .WaitFor(smartpoleSimulator)
    .WaitFor(energyHub)
    .WaitFor(commandCenterApi);

var operationsAgentApi = builder.AddProject<Projects.OperationsAgent_Api>("operationsagent-api")
    .WithReference(energyHub)
    .WithReference(commandCenterApi)
    .WaitFor(energyHub)
    .WaitFor(commandCenterApi);

builder.AddProject<Projects.CommandCenter_Web>("commandcenter-web")
    .WithReference(commandCenterApi)
    .WithReference(operationsAgentApi)
    .WaitFor(commandCenterApi)
    .WaitFor(operationsAgentApi)
    .WithExternalHttpEndpoints();

builder.AddProject<Projects.DemoControl_Web>("democontrol-web")
    .WithReference(demoScenarioApi)
    .WaitFor(demoScenarioApi)
    .WithExternalHttpEndpoints();

builder.Build().Run();
