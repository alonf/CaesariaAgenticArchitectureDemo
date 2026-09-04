#Requires -Version 7.0

<#
.SYNOPSIS
    Copies the platform outputs from a completed infrastructure deployment into the GitHub
    environment that the application workflow reads.

.DESCRIPTION
    deploy-infra.yml provisions the platform and prints its outputs; deploy-hosted-agent.yml consumes
    those values from the `vars` context. This script is the join between them, and it exists because
    the alternative is a human copying six strings out of a run summary - which is not reproducible,
    is silently wrong when a value changes, and is exactly the kind of undocumented manual step that
    makes a demo impossible to re-create.

    It is deliberately NOT a step inside deploy-infra.yml. Writing repository environment variables
    needs an Entra-independent GitHub credential with administration rights, and the workflow's
    built-in GITHUB_TOKEN cannot be granted it - `permissions:` has no environments scope. Making the
    pipeline able to do this would mean storing a long-lived personal access token as a secret, which
    is a materially worse trade than running one idempotent script as yourself after a deployment.

    Idempotent. Every value is read back and compared before it is written, so a second run against an
    unchanged deployment reports "already correct" and changes nothing.

.PARAMETER Environment
    The environment to read and to write: reads the deployment that produced resource group
    `rg-caesarea-<environment>`, writes the GitHub environment of the same name.

.PARAMETER SubscriptionId
    Subscription holding the deployment. Defaults to the current az account.

.PARAMETER Repository
    owner/name of the GitHub repository. Defaults to the origin remote of this working tree.

.PARAMETER DeploymentName
    Read this exact subscription deployment instead of discovering the most recent successful one.
    Use it to pin a known-good deployment, or when discovery is ambiguous.

.PARAMETER EnergyHubBaseUri
    Base URI of the Energy Hub, written as ENERGYHUB_BASE_URI. The hosted agent needs it and the
    platform template does not produce it, because the Energy Hub is an application deployment rather
    than part of the platform. Omit it and the variable is left alone with a note.

.EXAMPLE
    ./scripts/Sync-PlatformVariables.ps1 -Environment dev -WhatIf

.EXAMPLE
    ./scripts/Sync-PlatformVariables.ps1 -Environment dev

.EXAMPLE
    ./scripts/Sync-PlatformVariables.ps1 -Environment dev -DeploymentName caesarea-infra-33878263447
#>

[CmdletBinding(SupportsShouldProcess)]
param(
    [ValidateSet('dev', 'prod')]
    [string] $Environment = 'dev',
    [string] $SubscriptionId,
    [string] $Repository,
    [string] $DeploymentName,
    [string] $EnergyHubBaseUri
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# Deployments are named caesarea-infra-<github run id>, so the name alone cannot say which
# environment produced it. AZURE_RESOURCE_GROUP can, which is why discovery matches on the output
# rather than on the name.
$DeploymentNamePrefix = 'caesarea-infra-'

# Exactly the outputs the application workflow consumes. Anything else the deployment produces is
# reporting, and copying it here would create configuration nothing reads and nobody maintains.
$SyncedOutputs = @(
    'AZURE_RESOURCE_GROUP'
    'AZURE_CONTAINER_REGISTRY_ENDPOINT'
    'AZURE_CONTAINER_REGISTRY_NAME'
    'FOUNDRY_PROJECT_ENDPOINT'
    'MODEL_DEPLOYMENT_NAME'
    'AZURE_CONTAINER_APPS_ENVIRONMENT'
    'AZURE_CONTAINER_APPS_ENVIRONMENT_ID'
    'AZURE_SERVICES_IDENTITY_ID'
    'AZURE_SERVICES_IDENTITY_CLIENT_ID'
)

function Write-Step { param([string] $Message) Write-Host "`n=== $Message" -ForegroundColor Cyan }
function Write-Exists { param([string] $Message) Write-Host "  [exists] $Message" -ForegroundColor DarkGray }
function Write-Created { param([string] $Message) Write-Host "  [set] $Message" -ForegroundColor Green }
function Write-Note { param([string] $Message) Write-Host "  $Message" -ForegroundColor DarkGray }
function Write-Warn { param([string] $Message) Write-Host "  [skipped] $Message" -ForegroundColor Yellow }

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
        make this script overwrite a value it was never allowed to read. #>
    param([Parameter(Mandatory)] [string] $Path)

    $output = gh api $Path --silent 2>&1
    if ($LASTEXITCODE -eq 0) { return $true }
    if ("$output" -match '(?i)HTTP 404|Not Found') {
        # A handled 404 is an answer, not a failure - but it leaves $LASTEXITCODE at 1, and if the
        # last thing this script does is check for a variable that is absent, the script itself
        # exits 1 while reporting success. Anything running it in a && chain would stop there.
        $global:LASTEXITCODE = 0
        return $false
    }
    throw "Reading GitHub resource '$Path' failed: $($output -join [Environment]::NewLine)"
}

Write-Step 'Checking prerequisites'

foreach ($tool in @('az', 'gh')) {
    if (-not (Get-Command $tool -ErrorAction SilentlyContinue)) { throw "$tool is not on PATH." }
}

Invoke-Checked { gh auth status } 'Checking GitHub authentication' | Out-Null

if (-not $SubscriptionId) {
    $SubscriptionId = (Invoke-Checked { az account show --query id --output tsv } 'Reading the current Azure account').Trim()
}

if (-not $Repository) {
    $originUrl = Invoke-Checked { git -C "$PSScriptRoot/.." remote get-url origin } 'Reading the origin remote'
    $Repository = ("$originUrl" -replace '^.*github\.com[:/]', '' -replace '\.git$', '').Trim()
}

$expectedResourceGroup = "rg-caesarea-$Environment"

Write-Note "Subscription:   $SubscriptionId"
Write-Note "Repository:     $Repository"
Write-Note "Environment:    $Environment"
Write-Note "Resource group: $expectedResourceGroup"

# ---------------------------------------------------------------------------------------------
# Find the deployment whose outputs describe this environment.
# ---------------------------------------------------------------------------------------------

Write-Step 'Locating the deployment'

if ($DeploymentName) {
    $deployment = (Invoke-Checked {
        az deployment sub show --name $DeploymentName --subscription $SubscriptionId --output json
    } "Reading deployment '$DeploymentName'") | ConvertFrom-Json

    if ($deployment.properties.provisioningState -ne 'Succeeded') {
        throw "Deployment '$DeploymentName' is '$($deployment.properties.provisioningState)', not 'Succeeded'. Its outputs may be absent or stale; re-run the apply before syncing."
    }

    # The same resource-group check discovery makes, because naming a deployment explicitly is not a
    # claim about which environment produced it. Without this, passing a successful prod deployment
    # while -Environment says dev copies prod's endpoints into the dev environment, and both then
    # look entirely correct.
    $namedOutputs = $deployment.properties.outputs
    $namedResourceGroup = if ($namedOutputs -and $namedOutputs.PSObject.Properties.Name -contains 'AZURE_RESOURCE_GROUP') {
        $namedOutputs.AZURE_RESOURCE_GROUP.value
    }
    else { $null }

    if ($namedResourceGroup -ne $expectedResourceGroup) {
        throw "Deployment '$DeploymentName' produced resource group '$namedResourceGroup', not '$expectedResourceGroup'. It belongs to a different environment than -Environment $Environment."
    }
}
else {
    # Newest first, so the first match is the most recent successful deployment of this environment.
    $candidates = @(@(Invoke-Checked {
        az deployment sub list --subscription $SubscriptionId `
            --query "sort_by([?starts_with(name, '$DeploymentNamePrefix') && properties.provisioningState=='Succeeded'], &properties.timestamp) | reverse(@) | [].name" `
            --output tsv
    } 'Listing subscription deployments') | ForEach-Object { "$_".Trim() } | Where-Object { $_ })

    if ($candidates.Count -eq 0) {
        throw "No successful deployment named '$DeploymentNamePrefix*' was found in subscription $SubscriptionId. Run deploy-infra.yml with mode 'apply' first."
    }

    $deployment = $null
    foreach ($name in $candidates) {
        $candidate = (Invoke-Checked {
            az deployment sub show --name $name --subscription $SubscriptionId --output json
        } "Reading deployment '$name'") | ConvertFrom-Json

        $outputs = $candidate.properties.outputs
        if (-not $outputs) { continue }
        if ($outputs.PSObject.Properties.Name -notcontains 'AZURE_RESOURCE_GROUP') { continue }
        if ($outputs.AZURE_RESOURCE_GROUP.value -ne $expectedResourceGroup) { continue }

        $deployment = $candidate
        $DeploymentName = $name
        break
    }

    if (-not $deployment) {
        throw "Found $($candidates.Count) successful '$DeploymentNamePrefix*' deployment(s), none of which produced resource group '$expectedResourceGroup'. Deploy the '$Environment' environment, or pass -DeploymentName explicitly."
    }
}

Write-Note "Deployment:     $DeploymentName"
Write-Note "Completed:      $($deployment.properties.timestamp)"

$outputs = $deployment.properties.outputs
if (-not $outputs) {
    throw "Deployment '$DeploymentName' produced no outputs. It may have been run in what-if mode, or failed before the template completed."
}

# ARM camel-cases output names on the way back out: a template declaring AZURE_RESOURCE_GROUP is
# returned as `azurE_RESOURCE_GROUP`, FOUNDRY_PROJECT_ENDPOINT as `foundrY_PROJECT_ENDPOINT`. The
# lookups below survive it only because PowerShell compares property names and `-contains`
# case-insensitively. Anything ported to `jq` will match nothing and report every output missing.
$configuration = [ordered]@{}
$missing = @()

foreach ($name in $SyncedOutputs) {
    if ($outputs.PSObject.Properties.Name -notcontains $name) {
        $missing += $name
        continue
    }
    $configuration[$name] = "$($outputs.$name.value)"
}

if ($missing.Count -gt 0) {
    # A missing output means the deployment predates the template that produces it. Guessing a value
    # here would write a plausible string that the release pipeline would then fail on, far away from
    # the cause.
    throw "Deployment '$DeploymentName' is missing output(s): $($missing -join ', '). It was produced by an older template - re-run deploy-infra.yml with mode 'apply' and sync again."
}

# The Energy Hub is an application deployment, not part of the platform, so its address is supplied
# rather than read. Left absent when not passed: an empty value would be worse than no value, because
# the workflow would start and the agent would fail on its first tool call.
if ($EnergyHubBaseUri) {
    $configuration['ENERGYHUB_BASE_URI'] = $EnergyHubBaseUri
}

# ---------------------------------------------------------------------------------------------
# Write them.
# ---------------------------------------------------------------------------------------------

Write-Step "Writing variables to the '$Environment' environment"

if (-not (Test-GitHubResource "repos/$Repository/environments/$Environment")) {
    throw "GitHub environment '$Environment' does not exist on $Repository. Run ./scripts/Bootstrap-GitHubOidc.ps1 first - it creates the environment and the identity that deploys into it."
}

# Counted separately on purpose. Under -WhatIf, ShouldProcess declines every write, so a counter
# incremented inside it stays zero and the summary would announce that everything already matched -
# directly contradicting the "What if:" lines above it. $differing is what the comparison found;
# $written is what was actually done about it.
$differing = 0
$written = 0

foreach ($key in $configuration.Keys) {
    $value = $configuration[$key]

    # Compare before writing, so the report distinguishes "already correct" from "set". Both
    # converge; only one of them is a change.
    $current = $null
    if (Test-GitHubResource "repos/$Repository/environments/$Environment/variables/$key") {
        $current = (Invoke-Checked {
            gh api "repos/$Repository/environments/$Environment/variables/$key" --jq '.value'
        } "Reading variable '$key'").Trim()
    }

    if ($current -eq $value) {
        Write-Exists "$key"
        continue
    }

    $differing++

    if ($PSCmdlet.ShouldProcess("$Environment/$key", 'Set repository environment variable')) {
        Invoke-Checked {
            gh variable set $key --env $Environment --repo $Repository --body $value
        } "Setting variable '$key' on '$Environment'" | Out-Null
        Write-Created "$key = $value"
        $written++
    }
}

if (-not $configuration.Contains('ENERGYHUB_BASE_URI')) {
    Write-Warn 'ENERGYHUB_BASE_URI - not a platform output; pass -EnergyHubBaseUri once the Energy Hub is deployed'
}

Write-Step 'Done'

if ($differing -eq 0) {
    Write-Host "  All $($configuration.Count) variables already matched the deployment. Nothing changed." -ForegroundColor Green
}
elseif ($written -eq 0) {
    Write-Host "  $differing of $($configuration.Count) variable(s) differ from the deployment. Re-run without -WhatIf to write them." -ForegroundColor Yellow
}
else {
    Write-Host "  $written of $($configuration.Count) variable(s) updated on '$Environment'." -ForegroundColor Green
}

if ($differing -gt 0 -and $written -eq 0) { return }

Write-Host @"

  The '$Environment' environment now carries the platform's identity and addresses:

    - AZURE_CLIENT_ID, AZURE_TENANT_ID, AZURE_SUBSCRIPTION_ID   (from Bootstrap-GitHubOidc.ps1)
    - AZURE_RESOURCE_GROUP, AZURE_CONTAINER_REGISTRY_*,
      FOUNDRY_PROJECT_ENDPOINT, MODEL_DEPLOYMENT_NAME           (from this script)

  Next: deploy the agent into it.

      gh workflow run deploy-hosted-agent.yml -f environmentName=$Environment

  Re-run this script after any infrastructure apply that changes an address. It is idempotent, so
  running it when nothing has changed is free and reports exactly that.
"@ -ForegroundColor Green
