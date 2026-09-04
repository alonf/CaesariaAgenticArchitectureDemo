# The Foundry hosting platform runs linux/amd64 only. An ARM image builds without complaint and then
# fails to start, so build with: docker build --platform linux/amd64

# The runtime tag floats on purpose: patch releases carry security fixes and nothing in this image
# pins against them.
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble AS base
WORKDIR /app
# The port AgentHost listens on: PORT, which the platform injects and reserves, defaulting to 8088.
EXPOSE 8088

# The build tag does NOT float, and must stay in step with global.json. That file asks for 10.0.103
# with rollForward: latestPatch, which accepts 10.0.1xx and rejects everything else - so the moment
# MCR moved the `10.0-noble` tag to 10.0.400, a build that had worked for weeks failed with "A
# compatible .NET SDK was not found" and an SDK list showing a *newer* SDK than the one requested.
# Pin the toolchain, float the runtime. If you bump global.json, bump this line in the same commit.
FROM mcr.microsoft.com/dotnet/sdk:10.0.103-noble AS build
WORKDIR /src

# Restore against the project graph before copying sources, so a source-only change does not
# invalidate the restore layer.
COPY Directory.Build.props global.json ./
COPY Caesarea.ServiceDefaults/*.csproj Caesarea.ServiceDefaults/
COPY Shared/CanonicalModel/*.csproj Shared/CanonicalModel/
COPY Contracts/CommandCenter.Contracts/*.csproj Contracts/CommandCenter.Contracts/
COPY Contracts/DemoScenario.Contracts/*.csproj Contracts/DemoScenario.Contracts/
COPY Contracts/Energy.Contracts/*.csproj Contracts/Energy.Contracts/
COPY Contracts/OperationsAgent.Contracts/*.csproj Contracts/OperationsAgent.Contracts/
COPY Contracts/Security.Contracts/*.csproj Contracts/Security.Contracts/
COPY Contracts/Workforce.Contracts/*.csproj Contracts/Workforce.Contracts/
COPY Services/OperationsAgent.Api/*.csproj Services/OperationsAgent.Api/
COPY Services/OperationsAgent.Hosted/*.csproj Services/OperationsAgent.Hosted/
RUN dotnet restore Services/OperationsAgent.Hosted/OperationsAgent.Hosted.csproj

COPY Directory.Build.props global.json ./
COPY Caesarea.ServiceDefaults/ Caesarea.ServiceDefaults/
COPY Shared/ Shared/
COPY Contracts/ Contracts/
COPY Services/OperationsAgent.Api/ Services/OperationsAgent.Api/
COPY Services/OperationsAgent.Hosted/ Services/OperationsAgent.Hosted/
RUN dotnet publish Services/OperationsAgent.Hosted/OperationsAgent.Hosted.csproj \
    -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .

# Skills are files the agent discovers at runtime, so they ship in the image. SkillCatalog's
# relative-path resolution walks up looking for the solution file, which no container has - hence
# the absolute SKILLS_DIRECTORY below, matching where this copies them.
COPY skills/ /app/skills/
ENV SKILLS_DIRECTORY=/app/skills

ENTRYPOINT ["dotnet", "OperationsAgent.Hosted.dll"]
