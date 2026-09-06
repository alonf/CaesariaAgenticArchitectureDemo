using System.Diagnostics;
using Azure.AI.Projects;
using Azure.Core;
using OperationsAgent.Api.Services;

namespace Caesarea.Deterministic.Tests;

/// <summary>
/// The delegated consult owns an execution budget, and what happens when it expires is a contract.
/// <para>
/// This exercises the real <see cref="FoundryOperationsAgent"/> rather than the endpoint's fake, so
/// it covers the translation itself: a budget that expires mid-delegation has to surface as a timeout
/// the endpoint can map to 504. Unmapped, the cancellation escapes as a bare 500 and tells the
/// operator the service is broken when a peer was merely slow.
/// </para>
/// </summary>
public sealed class WorkforceConsultBudgetTests
{
    [Fact]
    public async Task AnExpiredBudgetDuringDelegationSurfacesAsATimeout()
    {
        // A peer that accepts the connection and then never answers. The delegation's own 60 second
        // budget is far longer than the agent's, so what trips here is the agent's, which is the
        // path the endpoint's 504 depends on.
        var agent = CreateAgent(TimeSpan.FromSeconds(1), new HangingHttpClientFactory());

        var timedOut = await Assert.ThrowsAsync<OperationsAgentTimedOutException>(
            () => agent.ConsultWorkforceAsync("Anything on L-417?", "corr-budget", CancellationToken.None));

        Assert.Contains("execution budget", timedOut.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CallerCancellationIsNotDisguisedAsATimeout()
    {
        // A caller that gives up is not a slow peer. Reporting it as a timeout would send someone
        // looking at the workforce domain for a request the browser abandoned.
        var agent = CreateAgent(TimeSpan.FromMinutes(5), new HangingHttpClientFactory());
        using var caller = new CancellationTokenSource();
        caller.CancelAfter(TimeSpan.FromSeconds(1));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => agent.ConsultWorkforceAsync("Anything on L-417?", "corr-abandon", caller.Token));
    }

    private static FoundryOperationsAgent CreateAgent(TimeSpan requestTimeout, IHttpClientFactory httpClientFactory)
    {
        // Nothing here reaches a network: the run ends inside the delegation, before any model call.
        var projectClient = new AIProjectClient(
            new Uri("https://caesarea.invalid/api/projects/test", UriKind.Absolute),
            new StubTokenCredential());

        var delegation = new WorkforceDelegation(
            httpClientFactory, NullLoggerFactory.Instance, NullLogger<WorkforceDelegation>.Instance);

        var clock = TimeProvider.System;
        var approvals = new PendingApprovalStore(clock, NullLogger<PendingApprovalStore>.Instance);
        var workItems = new SimulatedWorkItemGateway(clock, NullLogger<SimulatedWorkItemGateway>.Instance);
        var stageGate = new DemoStageGate(DemoStage.A2ADelegation);

        return new FoundryOperationsAgent(
            projectClient,
            CreateEnergyGateway(),
            new AgentSessionStore(clock),
            new SimulatedWorkKnowledgeSearch(clock, NullLogger<SimulatedWorkKnowledgeSearch>.Instance),
            new InMemoryCaseMemoryStore(clock, NullLogger<InMemoryCaseMemoryStore>.Instance),
            stageGate,
            new ToolSourceSwitch(),
            approvals,
            new RemediationWorkflowService(
                CreateEnergyGateway(),
                new FakeEnergyCommandGateway(),
                workItems,
                approvals,
                stageGate,
                clock,
                NullLoggerFactory.Instance,
                NullLogger<RemediationWorkflowService>.Instance),
            workItems,
            new FakeIncidentGateway(),
            new SecurityConsultSwitch(),
            delegation,
            httpClientFactory,
            new Uri("https://energy.invalid/mcp", UriKind.Absolute),
            new Uri("https://security.invalid/mcp", UriKind.Absolute),
            skillsDirectory: null,
            modelDeploymentName: "gpt-5.5",
            agentName: "Caesarea Operations Agent",
            maxFunctionIterations: 5,
            requestTimeout: requestTimeout,
            NullLoggerFactory.Instance,
            NullLogger<FoundryOperationsAgent>.Instance);
    }

    // Never read: the run ends inside the delegation, well before any tool could be selected.
    private static FakeEnergyReadGateway CreateEnergyGateway() => new()
    {
        State = new EnergyOperationalTwin(
            DemoAssets.StreetlightAssetId,
            DemoAssets.NorthPromenadeArea,
            false, false, true, false, false,
            ControllerHealthInfo.Healthy,
            null, null, false, null,
            DateTimeOffset.UtcNow,
            OperationalContext.None),
        Activity = []
    };

    private sealed class HangingHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) =>
            new(new HangingHandler()) { BaseAddress = new Uri("https://workforce.invalid/", UriKind.Absolute) };

        private sealed class HangingHandler : HttpMessageHandler
        {
            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                throw new UnreachableException();
            }
        }
    }

    private sealed class StubTokenCredential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            new("stub", DateTimeOffset.UtcNow.AddHours(1));

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            ValueTask.FromResult(GetToken(requestContext, cancellationToken));
    }
}
