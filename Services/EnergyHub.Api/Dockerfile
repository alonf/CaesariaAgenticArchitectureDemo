# The Energy Hub: the authoritative read model for city energy assets, and the only service the
# hosted agent talks to directly.
#
# Build from the repository root, not this directory - the restore below needs the whole project
# graph: docker build -f Services/EnergyHub.Api/Dockerfile .

# The runtime tag floats on purpose: patch releases carry security fixes and nothing here pins
# against them.
FROM mcr.microsoft.com/dotnet/aspnet:11.0-resolute AS base
WORKDIR /app
# The aspnet image listens here, and infra/apps.bicep points the container app's ingress at it.
EXPOSE 8080

# Pin the build SDK to global.json; latestFeature permits newer .NET 11 feature bands.
# Keep the explicit image version aligned with the minimum SDK when upgrading.
FROM mcr.microsoft.com/dotnet/sdk:11.0.100-resolute AS build
WORKDIR /src

# Restore against the project graph before copying sources, so a source-only change does not
# invalidate the restore layer.
COPY Directory.Build.props global.json ./
COPY Caesarea.ServiceDefaults/*.csproj Caesarea.ServiceDefaults/
COPY Shared/CanonicalModel/*.csproj Shared/CanonicalModel/
COPY Contracts/Energy.Contracts/*.csproj Contracts/Energy.Contracts/
COPY Contracts/SmartPole.Contracts/*.csproj Contracts/SmartPole.Contracts/
COPY Services/EnergyHub.Api/*.csproj Services/EnergyHub.Api/
RUN dotnet restore Services/EnergyHub.Api/EnergyHub.Api.csproj

COPY Caesarea.ServiceDefaults/ Caesarea.ServiceDefaults/
COPY Shared/ Shared/
COPY Contracts/Energy.Contracts/ Contracts/Energy.Contracts/
COPY Contracts/SmartPole.Contracts/ Contracts/SmartPole.Contracts/
COPY Services/EnergyHub.Api/ Services/EnergyHub.Api/
RUN dotnet publish Services/EnergyHub.Api/EnergyHub.Api.csproj \
    -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "EnergyHub.Api.dll"]
