# The Caesarea Workforce Agent, hosted by Foundry. Its container implements the Responses protocol
# only; Foundry supplies the public A2A endpoint and the agent card around it.
#
# The Foundry hosting platform runs linux/amd64 only. An ARM image builds without complaint and then
# fails to start, so build with: docker build --platform linux/amd64

# The runtime tag floats on purpose: patch releases carry security fixes and nothing here pins
# against them.
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble AS base
WORKDIR /app
# The port AgentHost listens on: PORT, which the platform injects and reserves, defaulting to 8088.
EXPOSE 8088

# The build tag does NOT float, and must stay in step with global.json - see the note in
# Services/OperationsAgent.Hosted/Dockerfile for what happens when it drifts.
FROM mcr.microsoft.com/dotnet/sdk:10.0.103-noble AS build
WORKDIR /src

# Restore against the project graph before copying sources, so a source-only change does not
# invalidate the restore layer.
COPY Directory.Build.props global.json ./
COPY Caesarea.ServiceDefaults/*.csproj Caesarea.ServiceDefaults/
COPY Shared/CanonicalModel/*.csproj Shared/CanonicalModel/
COPY Contracts/Workforce.Contracts/*.csproj Contracts/Workforce.Contracts/
COPY Services/WorkforceAgent.Api/*.csproj Services/WorkforceAgent.Api/
COPY Services/WorkforceAgent.Hosted/*.csproj Services/WorkforceAgent.Hosted/
RUN dotnet restore Services/WorkforceAgent.Hosted/WorkforceAgent.Hosted.csproj

COPY Caesarea.ServiceDefaults/ Caesarea.ServiceDefaults/
COPY Shared/ Shared/
COPY Contracts/Workforce.Contracts/ Contracts/Workforce.Contracts/
COPY Services/WorkforceAgent.Api/ Services/WorkforceAgent.Api/
COPY Services/WorkforceAgent.Hosted/ Services/WorkforceAgent.Hosted/
RUN dotnet publish Services/WorkforceAgent.Hosted/WorkforceAgent.Hosted.csproj \
    -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "WorkforceAgent.Hosted.dll"]
