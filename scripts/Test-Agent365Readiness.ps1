#Requires -Version 7.0

<#
.SYNOPSIS
    Verifies the tenant prerequisites for the Agent 365 governance segment, and collects the
    evidence the presenter shows.

.DESCRIPTION
    A read-only gate for W20's estate-governance beat. It verifies the chain the segment stands on:

      1. The hosted agent exists and the platform minted it an Entra identity.
      2. That identity is a first-class AGENT identity in the directory - the service principal
         comes back as @odata.type #microsoft.graph.agentIdentity with servicePrincipalType
         ServiceIdentity, which is the W20 claim ("the directory has a noun for this now") stated
         by the directory itself.
      3. The identity's owners, classified: user owners are accountable humans; a list containing
         only pipeline identities is the governance anti-pattern this segment teaches against
         (fix it with ./scripts/Set-AgentOwner.ps1 - or show it, then fix it live).
      4. At least one Agent 365 licence is assigned in the tenant. Without one, Agent 365 does not
         fail loudly: the portal loads and telemetry is quietly dropped.
      5. The tenant's agent-identity inventory, enumerated from the directory - the estate view,
         including every half-forgotten experiment, which is the point.

    What this script deliberately does NOT claim: whether the Agent 365 admin experience shows the
    agent. That is a portal walk behind an interactive sign-in; verify it in rehearsal, and keep a
    captured fallback labeled CAPTURED / NOT LIVE (see docs/prompts/13-agent365.md). An earlier
    probe found no Graph 'agentIdentities' collection at beta - one probe, one 404, recorded here
    rather than generalized into "no API exists".

.PARAMETER Environment
    Environment whose FOUNDRY_PROJECT_ENDPOINT to read, when one is not supplied directly.

.PARAMETER ProjectEndpoint
    Foundry project endpoint. Defaults to the environment's FOUNDRY_PROJECT_ENDPOINT variable.

.PARAMETER AgentName
    The hosted agent to verify.

.PARAMETER Repository
    owner/name of the GitHub repository, used only to read the endpoint default.

.PARAMETER RequireHumanOwner
    Fail (exit 1) when the agent identity has no user owner, instead of warning. Off by default so
    the presenter can keep the anti-pattern on display until the beat that fixes it.

.EXAMPLE
    ./scripts/Test-Agent365Readiness.ps1

.EXAMPLE
    ./scripts/Test-Agent365Readiness.ps1 -RequireHumanOwner
#>

[CmdletBinding()]
param(
    [ValidateSet('dev', 'prod')]
    [string] $Environment = 'dev',
    [string] $ProjectEndpoint,
    [string] $AgentName = 'caesarea-operations',
    [string] $Repository,
    [switch] $RequireHumanOwner
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$GraphBase = 'https://graph.microsoft.com/v1.0'

function Write-Step { param([string] $Message) Write-Host "`n=== $Message" -ForegroundColor Cyan }
function Write-Found { param([string] $Message) Write-Host "  [found] $Message" -ForegroundColor Green }
function Write-Missing { param([string] $Message) Write-Host "  [missing] $Message" -ForegroundColor Yellow }
function Write-Note { param([string] $Message) Write-Host "  $Message" -ForegroundColor DarkGray }
function Write-Warn { param([string] $Message) Write-Host "  $Message" -ForegroundColor Yellow }

function Invoke-Checked {
    param([Parameter(Mandatory)] [scriptblock] $Command, [Parameter(Mandatory)] [string] $What)

    $output = & $Command 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "$What failed (exit $LASTEXITCODE): $($output -join [Environment]::NewLine)"
    }
    return $output
}

$script:BearerTokens = @{}

function Get-BearerToken {
    param([Parameter(Mandatory)] [string] $Resource)

    if (-not $script:BearerTokens.ContainsKey($Resource)) {
        $script:BearerTokens[$Resource] = (Invoke-Checked {
            az account get-access-token --resource $Resource --query accessToken --output tsv
        } "Getting a token for $Resource").Trim()
    }
    return $script:BearerTokens[$Resource]
}

function Invoke-RestJson {
    <#  Native Invoke-WebRequest rather than `az rest`: the az launcher is a batch file on Windows,
        and an ampersand inside a Graph URL - every $select after a $filter - is enough to break
        its argument parsing mid-URL. Learned by watching it happen. #>
    param(
        [Parameter(Mandatory)] [string] $Url,
        [Parameter(Mandatory)] [string] $Resource,
        [hashtable] $ExtraHeaders = @{},
        [Parameter(Mandatory)] [string] $What
    )

    $headers = @{ Authorization = "Bearer $(Get-BearerToken -Resource $Resource)" } + $ExtraHeaders
    $response = Invoke-WebRequest -Uri $Url -Headers $headers -SkipHttpErrorCheck

    if ($response.StatusCode -ge 400) {
        throw "$What failed with HTTP $($response.StatusCode): $($response.Content)"
    }
    return $response.Content | ConvertFrom-Json
}

Write-Step 'Checking prerequisites'

if (-not (Get-Command az -ErrorAction SilentlyContinue)) { throw 'az is not on PATH.' }

if (-not $ProjectEndpoint) {
    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
        throw 'gh is not on PATH, so FOUNDRY_PROJECT_ENDPOINT cannot be read. Pass -ProjectEndpoint explicitly.'
    }
    if (-not $Repository) {
        $originUrl = Invoke-Checked { git -C "$PSScriptRoot/.." remote get-url origin } 'Reading the origin remote'
        $Repository = ("$originUrl" -replace '^.*github\.com[:/]', '' -replace '\.git$', '').Trim()
    }

    $value = gh api "repos/$Repository/environments/$Environment/variables/FOUNDRY_PROJECT_ENDPOINT" --jq '.value' 2>&1
    if ($LASTEXITCODE -ne 0) {
        $global:LASTEXITCODE = 0
        throw "FOUNDRY_PROJECT_ENDPOINT is not set on the '$Environment' environment. Run ./scripts/Sync-PlatformVariables.ps1 first, or pass -ProjectEndpoint."
    }
    $ProjectEndpoint = "$value".Trim()
}

Write-Note "Project: $ProjectEndpoint"
Write-Note "Agent:   $AgentName"

$failures = [System.Collections.Generic.List[string]]::new()

# ---------------------------------------------------------------------------------------------
# 1. The agent, and the identity the platform minted for it.
# ---------------------------------------------------------------------------------------------

Write-Step "Hosted agent '$AgentName'"

$agent = Invoke-RestJson -Url "$ProjectEndpoint/agents/$AgentName`?api-version=v1" -Resource 'https://ai.azure.com' -What "Reading agent '$AgentName'"
$principalId = "$($agent.instance_identity.principal_id)"

if ($principalId) {
    Write-Found "agent exists; platform-minted identity $principalId"
}
else {
    $failures.Add("Agent '$AgentName' has no instance identity. Deploy a version first (deploy-hosted-agent.yml).")
}

# The card description is what the Agent 365 registry's About panel shows a governance reviewer.
# Blank reads as "nobody will say what this does" - which is a warning, not a failure, because
# the segment can show the gap and deploy-hosted-agent.yml fills it on the next release.
$cardDescription = if ($agent.PSObject.Properties.Name -contains 'agent_card' -and $agent.agent_card) { "$($agent.agent_card.description)" } else { '' }
if ($cardDescription -and $cardDescription.Length -gt 20) {
    Write-Found 'agent card carries a description for the registry''s About panel'
}
else {
    Write-Warn "The agent card description is blank or a placeholder ('$cardDescription'). The registry will show 'No description provided'; deploy-hosted-agent.yml sets it on the next release."
}

# ---------------------------------------------------------------------------------------------
# 2. What the directory says that identity IS.
# ---------------------------------------------------------------------------------------------

if ($principalId) {
    Write-Step 'The identity in Microsoft Entra'

    $servicePrincipal = Invoke-RestJson -Url "$GraphBase/servicePrincipals/$principalId" -Resource 'https://graph.microsoft.com' -What 'Reading the agent service principal'
    $odataType = "$($servicePrincipal.'@odata.type')"

    Write-Note "displayName: $($servicePrincipal.displayName)"
    Write-Note "created:     $($servicePrincipal.createdDateTime)"

    if ($odataType -eq '#microsoft.graph.agentIdentity') {
        # The lecture line, stated by the directory itself rather than by a slide.
        Write-Found "the directory types it as an AGENT identity ($odataType, servicePrincipalType $($servicePrincipal.servicePrincipalType))"
    }
    else {
        $failures.Add("The agent's service principal is '$odataType', not #microsoft.graph.agentIdentity. The Entra Agent ID story has changed shape - inspect before presenting.")
    }

    if (-not $servicePrincipal.accountEnabled) {
        $failures.Add('The agent identity is disabled.')
    }

    # ---------------------------------------------------------------------------------------------
    # 3. Who answers for it.
    # ---------------------------------------------------------------------------------------------

    Write-Step 'Owners (who answers for this agent)'

    $owners = @((Invoke-RestJson -Url "$GraphBase/servicePrincipals/$principalId/owners" -Resource 'https://graph.microsoft.com' -What 'Reading the identity owners').value)
    $humanOwners = @($owners | Where-Object { "$($_.'@odata.type')" -eq '#microsoft.graph.user' })

    if ($owners.Count -eq 0) {
        Write-Missing 'no owners at all'
    }
    foreach ($owner in $owners) {
        $kind = if ("$($owner.'@odata.type')" -eq '#microsoft.graph.user') { 'user' } else { 'service principal' }
        Write-Note "- $($owner.displayName) ($kind)"
    }

    if ($humanOwners.Count -gt 0) {
        Write-Found "accountable human owner: $(($humanOwners | ForEach-Object { $_.displayName }) -join ', ')"
    }
    else {
        $message = 'No HUMAN owner: whoever created the first version owns the identity, and here that was a pipeline. This is the anti-pattern the segment teaches - show it, then fix it live with ./scripts/Set-AgentOwner.ps1.'
        if ($RequireHumanOwner) {
            $failures.Add($message)
        }
        else {
            Write-Warn $message
        }
    }
}

# ---------------------------------------------------------------------------------------------
# 4. The licence Agent 365 needs before it governs anything.
# ---------------------------------------------------------------------------------------------

Write-Step 'Agent 365 licensing'

$skus = @((Invoke-RestJson -Url "$GraphBase/subscribedSkus" -Resource 'https://graph.microsoft.com' -What 'Listing subscribed SKUs').value)
$agent365Skus = @($skus | Where-Object { "$($_.skuPartNumber)" -like '*AGENT_365*' })

if ($agent365Skus.Count -eq 0) {
    $failures.Add('No Agent 365 SKU in this tenant. Nothing to license against; the registry beat has no live path.')
}
else {
    foreach ($sku in $agent365Skus) {
        Write-Note "$($sku.skuPartNumber): $($sku.consumedUnits) of $($sku.prepaidUnits.enabled) assigned"
    }

    $assigned = ($agent365Skus | Measure-Object -Property consumedUnits -Sum).Sum
    if ($assigned -ge 1) {
        Write-Found "$assigned Agent 365 licence(s) assigned - telemetry has somewhere to go"
    }
    else {
        $failures.Add('An Agent 365 SKU exists but no licence is assigned. Run ./scripts/Assign-Agent365License.ps1 - without one, telemetry is quietly dropped.')
    }
}

# ---------------------------------------------------------------------------------------------
# 5. The estate: every agent identity in the tenant.
# ---------------------------------------------------------------------------------------------

Write-Step 'Agent-identity inventory (the estate view)'

$inventory = Invoke-RestJson `
    -Url "$GraphBase/servicePrincipals?`$filter=servicePrincipalType eq 'ServiceIdentity'&`$select=id,displayName,createdDateTime&`$count=true&`$top=25" `
    -Resource 'https://graph.microsoft.com' `
    -ExtraHeaders @{ ConsistencyLevel = 'eventual' } `
    -What 'Enumerating agent identities'

$agents = @($inventory.value)
Write-Note "$($inventory.'@odata.count') agent identit(ies) in this tenant:"
foreach ($identity in $agents | Sort-Object createdDateTime -Descending) {
    Write-Note "- $($identity.displayName)  ($($identity.createdDateTime))"
}
Write-Note 'Old experiments in this list are not noise - they are the estate-governance argument.'

# ---------------------------------------------------------------------------------------------
# Verdict.
# ---------------------------------------------------------------------------------------------

Write-Step 'Verdict'

if ($failures.Count -gt 0) {
    foreach ($failure in $failures) { Write-Missing $failure }
    exit 1
}

Write-Found 'the tenant is ready for the Agent 365 segment'
Write-Host @"
  What this proves: the agent is a first-class identity in the directory, licensed for governance.
  What it does not prove: the Agent 365 admin experience itself - that is a portal walk behind an
  interactive sign-in. Verify it in rehearsal and keep the captured fallback current
  (docs/prompts/13-agent365.md), labeled CAPTURED / NOT LIVE.
"@ -ForegroundColor Green
exit 0
