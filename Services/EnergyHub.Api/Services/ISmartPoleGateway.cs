using Caesarea.Contracts;

namespace EnergyHub.Api.Services;

public interface ISmartPoleGateway
{
    Task<SmartPolePhysicalState> GetStateAsync(string assetId, string correlationId, CancellationToken cancellationToken);

    Task<SmartPoleCommandResult> SetLampStateAsync(SetLampStateCommand command, string correlationId, CancellationToken cancellationToken);
}
