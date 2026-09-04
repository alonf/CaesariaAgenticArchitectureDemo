# The SmartPole simulator: the device layer the Energy Hub reads from. Internal ingress only - see
# infra/apps.bicep. Nothing outside the Container Apps environment calls this.
#
# Build from the repository root, not this directory:
#   docker build -f Services/SmartPole.Simulator.Api/Dockerfile .

# The runtime tag floats on purpose: patch releases carry security fixes and nothing here pins
# against them.
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble AS base
WORKDIR /app
# The aspnet image listens here, and infra/apps.bicep points the container app's ingress at it.
EXPOSE 8080

# The build tag does NOT float, and must stay in step with global.json - see the note in
# Services/EnergyHub.Api/Dockerfile for what happens when it drifts.
FROM mcr.microsoft.com/dotnet/sdk:10.0.103-noble AS build
WORKDIR /src

# Restore against the project graph before copying sources, so a source-only change does not
# invalidate the restore layer.
COPY Directory.Build.props global.json ./
COPY Caesarea.ServiceDefaults/*.csproj Caesarea.ServiceDefaults/
COPY Shared/CanonicalModel/*.csproj Shared/CanonicalModel/
COPY Contracts/SmartPole.Contracts/*.csproj Contracts/SmartPole.Contracts/
COPY Services/SmartPole.Simulator.Api/*.csproj Services/SmartPole.Simulator.Api/
RUN dotnet restore Services/SmartPole.Simulator.Api/SmartPole.Simulator.Api.csproj

COPY Caesarea.ServiceDefaults/ Caesarea.ServiceDefaults/
COPY Shared/ Shared/
COPY Contracts/SmartPole.Contracts/ Contracts/SmartPole.Contracts/
COPY Services/SmartPole.Simulator.Api/ Services/SmartPole.Simulator.Api/
RUN dotnet publish Services/SmartPole.Simulator.Api/SmartPole.Simulator.Api.csproj \
    -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "SmartPole.Simulator.Api.dll"]
