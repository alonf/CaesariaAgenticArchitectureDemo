#Requires -Version 7.0

<#
.SYNOPSIS
    Registers the Energy Hub as a protected API in Microsoft Entra, and records its identifiers on
    the GitHub environment.

.DESCRIPTION
    The Energy Hub has to be reachable from the public internet, because the hosted agent runs in a
    Foundry sandbox that is not in this VNet. Reachable is not the same as open: it maps an MCP
    server, demo breakpoints and an admin surface that can reset the whole scenario, and none of
    that belongs to anonymous callers.

    Container Apps' built-in authentication rejects unauthenticated requests at the ingress, but only
    if there is something to validate them against. That is what this script creates:

      - an application registration representing the API, whose Application ID URI (api://<appId>)
        becomes the audience the ingress accepts;
      - an application role, EnergyHub.Read, assignable to applications rather than users - the agent
        is a workload with a managed identity, not a person;
      - the service principal that makes the role assignable in this tenant.

    It assigns that role to nobody. The agent's identity does not exist until its first version is
    deployed, so the grant belongs to deploy-hosted-agent.yml, which reads the principal back and
    binds it. See docs/deployment.md.

    Idempotent. Existing objects are reused, the app role keeps the ID it already has - changing it
    would revoke every grant made against it - and every value is compared before it is written.

.PARAMETER Environment
    Environment to register the API for. Objects are named <prefix>-<environment>.

.PARAMETER Repository
    owner/name of the GitHub repository. Defaults to the origin remote of this working tree.

.PARAMETER ApplicationNamePrefix
    Display-name prefix for the application registration.

.PARAMETER SkipGitHub
    Create the Entra objects but do not write the GitHub environment variables.

.EXAMPLE
    ./scripts/Bootstrap-EnergyHubApi.ps1 -Environment dev -WhatIf

.EXAMPLE
    ./scripts/Bootstrap-EnergyHubApi.ps1 -Environment dev
#>

[CmdletBinding(SupportsShouldProcess)]
param(
    [ValidateSet('dev', 'prod')]
    [string] $Environment = 'dev',
    [string] $Repository,
    [string] $ApplicationNamePrefix = 'caesarea-energyhub-api',
    [switch] $SkipGitHub
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$AppRoleValue = 'EnergyHub.Read'
$GraphBase = 'https://graph.microsoft.com/v1.0'

function Write-Step { param([string] $Message) Write-Host "`n=== $Message" -ForegroundColor Cyan }
function Write-Exists { param([string] $Message) Write-Host "  [exists] $Message" -ForegroundColor DarkGray }
function Write-Created { param([string] $Message) Write-Host "  [created] $Message" -ForegroundColor Green }
function Write-Note { param([string] $Message) Write-Host "  $Message" -ForegroundColor DarkGray }

function Invoke-Checked {
    param([Parameter(Mandatory)] [scriptblock] $Command, [Parameter(Mandatory)] [string] $What)

    $output = & $Command 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "$What failed (exit $LASTEXITCODE): $($output -join [Environment]::NewLine)"
    }
    return $output
}

function Test-GitHubResource {
    <#  True when it exists, false on a genuine 404, throws otherwise. Treating a 403 as absent would
        let this script overwrite a value it was never allowed to read. #>
    param([Parameter(Mandatory)] [string] $Path)

    $output = gh api $Path --silent 2>&1
    if ($LASTEXITCODE -eq 0) { return $true }
    if ("$output" -match '(?i)HTTP 404|Not Found') {
        # A handled 404 is an answer, not a failure - but it leaves $LASTEXITCODE at 1, and a script
        # whose last act is checking for an absent variable would then exit 1 while reporting success.
        $global:LASTEXITCODE = 0
        return $false
    }
    throw "Reading GitHub resource '$Path' failed: $($output -join [Environment]::NewLine)"
}

function Invoke-Graph {
    <#  Graph through `az rest`, with the body written to a file. Passing JSON inline through az on
        Windows means fighting two layers of quoting, and the failures are silent truncations rather
        than errors. #>
    param(
        [Parameter(Mandatory)] [string] $Method,
        [Parameter(Mandatory)] [string] $Url,
        [object] $Body,
        [Parameter(Mandatory)] [string] $What
    )

    if ($null -eq $Body) {
        $output = Invoke-Checked { az rest --method $Method --url $Url --output json } $What
    }
    else {
        $file = New-TemporaryFile
        try {
            ($Body | ConvertTo-Json -Depth 10) | Set-Content -Path $file -Encoding utf8
            $output = Invoke-Checked {
                az rest --method $Method --url $Url --headers 'Content-Type=application/json' --body "@$file" --output json
            } $What
        }
        finally {
            Remove-Item $file -ErrorAction SilentlyContinue
        }
    }

    $text = "$output".Trim()
    if ([string]::IsNullOrWhiteSpace($text)) { return $null }
    return $text | ConvertFrom-Json
}

Write-Step 'Checking prerequisites'

foreach ($tool in @('az')) {
    if (-not (Get-Command $tool -ErrorAction SilentlyContinue)) { throw "$tool is not on PATH." }
}
if (-not $SkipGitHub -and -not (Get-Command 'gh' -ErrorAction SilentlyContinue)) {
    throw 'gh is not on PATH. Install it, or pass -SkipGitHub and set the variables yourself.'
}

$account = (Invoke-Checked { az account show --output json } 'Reading the current Azure account') | ConvertFrom-Json

if (-not $Repository -and -not $SkipGitHub) {
    $originUrl = Invoke-Checked { git -C "$PSScriptRoot/.." remote get-url origin } 'Reading the origin remote'
    $Repository = ("$originUrl" -replace '^.*github\.com[:/]', '' -replace '\.git$', '').Trim()
}

$applicationName = "$ApplicationNamePrefix-$Environment"

Write-Note "Tenant:      $($account.tenantId)"
Write-Note "Application: $applicationName"
if (-not $SkipGitHub) { Write-Note "Repository:  $Repository" }

# ---------------------------------------------------------------------------------------------
# The application registration.
# ---------------------------------------------------------------------------------------------

Write-Step "Application registration '$applicationName'"

$existing = @(@(Invoke-Checked {
    az ad app list --display-name $applicationName --query "[].{appId:appId,id:id}" --output json
} "Listing applications named '$applicationName'") | ConvertFrom-Json)

if ($existing.Count -gt 1) {
    throw "Found $($existing.Count) applications named '$applicationName'. Entra permits duplicate display names, so this script refuses to guess - delete the extras, or rename them."
}

if ($existing.Count -eq 1) {
    $appId = $existing[0].appId
    $objectId = $existing[0].id
    Write-Exists "application ($appId)"
}
elseif ($PSCmdlet.ShouldProcess($applicationName, 'Create Entra application registration')) {
    $created = Invoke-Graph -Method post -Url "$GraphBase/applications" -What "Creating application '$applicationName'" -Body @{
        displayName    = $applicationName
        signInAudience = 'AzureADMyOrg'
    }
    $appId = $created.appId
    $objectId = $created.id
    Write-Created "application ($appId)"

    # Entra takes a moment to make a new application readable by its own object ID.
    Start-Sleep -Seconds 10
}
else {
    Write-Note 'Skipped; nothing further can be done without the application.'
    return
}

# ---------------------------------------------------------------------------------------------
# Identifier URI, token version and the application role.
# ---------------------------------------------------------------------------------------------

Write-Step 'API surface'

$app = Invoke-Graph -Method get -Url "$GraphBase/applications/$objectId" -What 'Reading the application'

$identifierUri = "api://$appId"
$currentRoles = @($app.appRoles | Where-Object { $_.value -eq $AppRoleValue })

# The role keeps whatever ID it already has. Regenerating it would silently revoke every assignment
# made against the old one, and the agent would start failing with a 403 that points nowhere.
$roleId = if ($currentRoles.Count -gt 0) { $currentRoles[0].id } else { [guid]::NewGuid().ToString() }

$desiredRole = @{
    id                 = $roleId
    allowedMemberTypes = @('Application')
    displayName        = $AppRoleValue
    value              = $AppRoleValue
    description        = 'Read the authoritative state of Caesarea energy assets.'
    isEnabled          = $true
}

$needsUri = ($app.identifierUris -notcontains $identifierUri)
# Access token version 2 is not cosmetic. Container Apps validates against the v2.0 issuer
# (https://login.microsoftonline.com/<tenant>/v2.0); a v1 token carries issuer sts.windows.net and is
# rejected, with a 401 that says nothing about why.
$needsTokenVersion = ($null -eq $app.api) -or ($app.api.requestedAccessTokenVersion -ne 2)
$needsRole = ($currentRoles.Count -eq 0) -or
             ($currentRoles[0].isEnabled -ne $true) -or
             ($currentRoles[0].description -ne $desiredRole.description)

if (-not ($needsUri -or $needsTokenVersion -or $needsRole)) {
    Write-Exists "identifier URI, token version 2 and role '$AppRoleValue'"
}
elseif ($PSCmdlet.ShouldProcess($applicationName, 'Update identifier URI, token version and application role')) {
    $otherRoles = @($app.appRoles | Where-Object { $_.value -ne $AppRoleValue })

    Invoke-Graph -Method patch -Url "$GraphBase/applications/$objectId" -What 'Updating the application' -Body @{
        identifierUris = @($identifierUri)
        api            = @{ requestedAccessTokenVersion = 2 }
        appRoles       = @($otherRoles + $desiredRole)
    } | Out-Null

    Write-Created "identifier URI $identifierUri, access token version 2, role '$AppRoleValue' ($roleId)"
}

# ---------------------------------------------------------------------------------------------
# The service principal. Without it the role exists on paper and cannot be assigned to anything.
# ---------------------------------------------------------------------------------------------

Write-Step 'Service principal'

$servicePrincipals = @(@(Invoke-Checked {
    az ad sp list --filter "appId eq '$appId'" --query "[].id" --output json
} 'Listing the service principal') | ConvertFrom-Json)

if ($servicePrincipals.Count -gt 0) {
    Write-Exists "service principal ($($servicePrincipals[0]))"
}
elseif ($PSCmdlet.ShouldProcess($applicationName, 'Create service principal')) {
    $sp = Invoke-Graph -Method post -Url "$GraphBase/servicePrincipals" -What 'Creating the service principal' -Body @{
        appId = $appId
    }
    Write-Created "service principal ($($sp.id))"
}

# ---------------------------------------------------------------------------------------------
# Hand the identifiers to the pipelines.
# ---------------------------------------------------------------------------------------------

if (-not $SkipGitHub) {
    Write-Step "Writing variables to the '$Environment' environment"

    if (-not (Test-GitHubResource "repos/$Repository/environments/$Environment")) {
        throw "GitHub environment '$Environment' does not exist on $Repository. Run ./scripts/Bootstrap-GitHubOidc.ps1 first."
    }

    $configuration = [ordered]@{
        ENERGYHUB_API_CLIENT_ID   = $appId
        ENERGYHUB_API_APP_ROLE_ID = $roleId
        ENERGYHUB_API_SCOPE       = "$identifierUri/.default"
    }

    $differing = 0
    $written = 0

    foreach ($key in $configuration.Keys) {
        $current = $null
        if (Test-GitHubResource "repos/$Repository/environments/$Environment/variables/$key") {
            $current = (Invoke-Checked {
                gh api "repos/$Repository/environments/$Environment/variables/$key" --jq '.value'
            } "Reading variable '$key'").Trim()
        }

        if ($current -eq $configuration[$key]) {
            Write-Exists $key
            continue
        }

        $differing++

        if ($PSCmdlet.ShouldProcess("$Environment/$key", 'Set repository environment variable')) {
            Invoke-Checked {
                gh variable set $key --env $Environment --repo $Repository --body $configuration[$key]
            } "Setting variable '$key'" | Out-Null
            Write-Created "$key = $($configuration[$key])"
            $written++
        }
    }

    if ($differing -eq 0) { Write-Note 'All three variables already matched.' }
    elseif ($written -eq 0) { Write-Note "$differing variable(s) differ. Re-run without -WhatIf to write them." }
}

Write-Step 'Done'
Write-Host @"
  The Energy Hub is registered as a protected API.

    Application ID   $appId
    Audience         api://$appId
    App role         $AppRoleValue ($roleId)

  Nothing holds that role yet, and that is correct: the agent's identity is created by the platform
  when its first version is deployed, so deploy-hosted-agent.yml grants it there.

  Next:
    1. Deploy the services            gh workflow run deploy-services.yml -f environmentName=$Environment
    2. Point the agent at the Hub     ./scripts/Sync-PlatformVariables.ps1 -Environment $Environment -EnergyHubBaseUri <fqdn>
    3. Redeploy the agent             gh workflow run deploy-hosted-agent.yml -f environmentName=$Environment
"@ -ForegroundColor Green
