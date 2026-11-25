var builder = DistributedApplication.CreateBuilder(args);

var smartpoleSimulator = builder.AddDotnetProject("smartpole-simulator-api", "../Services/SmartPole.Simulator.Api/SmartPole.Simulator.Api.csproj");

var energyHub = builder.AddDotnetProject("energyhub-api", "../Services/EnergyHub.Api/EnergyHub.Api.csproj")
    .WithReference(smartpoleSimulator)
    .WaitFor(smartpoleSimulator);

var commandCenterApi = builder.AddDotnetProject("commandcenter-api", "../Services/CommandCenter.Api/CommandCenter.Api.csproj")
    .WithReference(energyHub)
    .WaitFor(energyHub);

// The Security domain: an authoritative hub whose records only its own agent may read, and the
// agent that reads them. The Operations Agent deliberately has no reference to the hub.
var securityHub = builder.AddDotnetProject("securityhub-api", "../Services/SecurityHub.Api/SecurityHub.Api.csproj");

var securityAgentApi = builder.AddDotnetProject("securityagent-api", "../Services/SecurityAgent.Api/SecurityAgent.Api.csproj")
    .WithReference(securityHub)
    .WaitFor(securityHub);

// The workforce domain: the work-order system of record, and the only agent permitted to read it.
// The Operations Agent has no reference to the hub - it consults the agent over A2A instead.
var workforceHub = builder.AddDotnetProject("workforcehub-api", "../Services/WorkforceHub.Api/WorkforceHub.Api.csproj");

var workforceAgentApi = builder.AddDotnetProject("workforceagent-api", "../Services/WorkforceAgent.Api/WorkforceAgent.Api.csproj")
    .WithReference(workforceHub)
    .WaitFor(workforceHub);

var operationsAgentApi = builder.AddDotnetProject("operationsagent-api", "../Services/OperationsAgent.Api/OperationsAgent.Api.csproj")
    .WithReference(energyHub)
    .WithReference(commandCenterApi)
    .WithReference(securityAgentApi)
    .WithReference(workforceAgentApi)
    .WaitFor(energyHub);

// DemoScenario references the Operations Agent (to propagate stage changes) but does not wait for
// it, so an agent startup problem never blocks the deterministic Stage 0 lecture path.
var demoScenarioApi = builder.AddDotnetProject("demoscenario-api", "../Services/DemoScenario.Api/DemoScenario.Api.csproj")
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
// The switchboard also addresses the Energy Hub and the two peer agents directly, because demo
// snippets live in the service whose code they pause and each is armed at its own boundary. Its
// reference to the Workforce Hub is different in kind: it reads the work orders in full, which is
// the presenter's own view of what the domain withheld, and is why that route is loopback only.
var demoControlWeb = builder.AddDotnetProject("democontrol-web", "../Apps/DemoControl.Web/DemoControl.Web.csproj")
    .WithReference(demoScenarioApi)
    .WithReference(operationsAgentApi)
    .WithReference(energyHub)
    .WithReference(securityAgentApi)
    .WithReference(workforceAgentApi)
    .WithReference(workforceHub)
    .WaitFor(demoScenarioApi)
    .WithExternalHttpEndpoints();

builder.AddDotnetProject("commandcenter-web", "../Apps/CommandCenter.Web/CommandCenter.Web.csproj")
    .WithReference(commandCenterApi)
    .WithReference(operationsAgentApi)
    .WithReference(demoControlWeb)
    .WaitFor(commandCenterApi)
    .WithExternalHttpEndpoints();

await builder.Build().RunAsync();
