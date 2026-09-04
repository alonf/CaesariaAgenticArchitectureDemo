# The Foundry hosting platform runs linux/amd64 only. An ARM image builds without complaint and then
# fails to start, so build with: docker build --platform linux/amd64
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble AS base
WORKDIR /app
# The platform routes to this port; the container needs no public one of its own.
EXPOSE 8088

FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build
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
