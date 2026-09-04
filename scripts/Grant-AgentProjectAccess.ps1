#Requires -Version 7.0

<#
.SYNOPSIS
    Grants the hosted agent's identity the Foundry User role on the account and the project.

.DESCRIPTION
    A hosted agent calls the project data plane as itself - for models, for its own session storage,
    and for any toolbox it reaches. Without Foundry User it fails at runtime with

        Principal does not have access to API/Operation

    which reads like a broken endpoint rather than a missing role assignment.

    The Work IQ guidance is explicit that this is needed at **both** account and project scope, so
    both are granted here. Project scope alone is the tempting least-privilege reading and it is not
    what the platform checks.

    This is a script rather than part of infra/modules/rbac.bicep for the same reason the Energy Hub
    grant is: the agent's identity is created by the platform when its first version is deployed, so
    it does not exist at provisioning time and cannot be bound declaratively.

    Idempotent: existing assignments are reported and left alone.

.PARAMETER Environment
    Environment whose project and agent to wire up.

.PARAMETER AgentName
    Name of the hosted agent whose identity is granted access.

.PARAMETER SubscriptionId
    Subscription holding the Foundry account. Defaults to the current az account.

.PARAMETER ResourceGroupName
    Resource group holding the Foundry account. Defaults to rg-caesarea-<environment>.

.EXAMPLE
    ./scripts/Grant-AgentProjectAccess.ps1 -WhatIf

.EXAMPLE
    ./scripts/Grant-AgentProjectAccess.ps1 -Environment dev
#>

[CmdletBinding(SupportsShouldProcess)]
param(
    [ValidateSet('dev', 'prod')]
    [string] $Environment = 'dev',
    [string] $AgentName = 'caesarea-operations',
    [string] $SubscriptionId,
    [string] $ResourceGroupName
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# Call agents and models at runtime. What a running workload needs, and nothing more.
$FoundryUserRoleId = '53ca6127-db72-4b80-b1b0-d745d6d5456d'

if (-not $ResourceGroupName) { $ResourceGroupName = "rg-caesarea-$Environment" }

function Write-Step { param([string] $Message) Write-Host "`n=== $Message" -ForegroundColor Cyan }
function Write-Exists { param([string] $Message) Write-Host "  [exists] $Message" -ForegroundColor DarkGray }
function Write-Created { param([string] $Message) Write-Host "  [granted] $Message" -ForegroundColor Green }
function Write-Note { param([string] $Message) Write-Host "  $Message" -ForegroundColor DarkGray }

function Invoke-Checked {
    param([Parameter(Mandatory)] [scriptblock] $Command, [Parameter(Mandatory)] [string] $What)

    $output = & $Command 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "$What failed (exit $LASTEXITCODE): $($output -join [Environment]::NewLine)"
    }
    return $output
}

function Invoke-Arm {
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
        finally { Remove-Item $file -ErrorAction SilentlyContinue }
    }

    $text = "$output".Trim()
    if ([string]::IsNullOrWhiteSpace($text)) { return $null }
    return $text | ConvertFrom-Json
}

Write-Step 'Checking prerequisites'

if (-not (Get-Command az -ErrorAction SilentlyContinue)) { throw 'az is not on PATH.' }

$account = (Invoke-Checked { az account show --output json } 'Reading the current Azure account') | ConvertFrom-Json
if (-not $SubscriptionId) { $SubscriptionId = $account.id }

$accounts = @(@(Invoke-Checked {
    az cognitiveservices account list --resource-group $ResourceGroupName --subscription $SubscriptionId --query "[?kind=='AIServices'].name" --output tsv
} 'Listing Foundry accounts') | ForEach-Object { "$_".Trim() } | Where-Object { $_ })

if ($accounts.Count -ne 1) {
    throw "Expected exactly one AIServices account in '$ResourceGroupName'; found $($accounts.Count)."
}

$accountName = $accounts[0]
$projectName = "caesarea-$Environment"
$projectEndpoint = "https://$accountName.services.ai.azure.com/api/projects/$projectName"

Write-Note "Foundry: $accountName / $projectName"
Write-Note "Agent:   $AgentName"

# ---------------------------------------------------------------------------------------------
# The agent's identity, which exists only once a version has been deployed.
# ---------------------------------------------------------------------------------------------

Write-Step 'Agent identity'

$token = (Invoke-Checked {
    az account get-access-token --resource https://ai.azure.com --query accessToken --output tsv
} 'Getting a Foundry data-plane token').Trim()

$response = Invoke-WebRequest -Uri "$projectEndpoint/agents/$AgentName`?api-version=v1" `
    -Headers @{ Authorization = "Bearer $token" } -SkipHttpErrorCheck

if ($response.StatusCode -eq 404) {
    throw "Agent '$AgentName' does not exist. Deploy it first: gh workflow run deploy-hosted-agent.yml -f environmentName=$Environment"
}
if ($response.StatusCode -ne 200) {
    throw "Reading agent '$AgentName' failed with HTTP $($response.StatusCode): $($response.Content)"
}

$principalId = ($response.Content | ConvertFrom-Json).instance_identity.principal_id
if (-not $principalId) {
    throw "Agent '$AgentName' has no instance identity yet. Wait for its version to become active and retry."
}

Write-Note "Principal: $principalId"

# ---------------------------------------------------------------------------------------------
# Both scopes. The platform checks both; project alone is not enough.
# ---------------------------------------------------------------------------------------------

$scopes = [ordered]@{
    'account' = "/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroupName/providers/Microsoft.CognitiveServices/accounts/$accountName"
    'project' = "/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroupName/providers/Microsoft.CognitiveServices/accounts/$accountName/projects/$projectName"
}

foreach ($label in $scopes.Keys) {
    $scope = $scopes[$label]
    Write-Step "Foundry User at $label scope"

    # No $filter in the URL. az is a batch file on Windows, so cmd re-parses the argument and splits
    # it at the '&' before the query parameter - the call succeeds and the shell then reports
    # "'$filter' is not recognized as an internal or external command". Filtering here avoids it.
    $existing = Invoke-Arm -Method get -What "Listing assignments at $label scope" `
        -Url "https://management.azure.com$scope/providers/Microsoft.Authorization/roleAssignments?api-version=2022-04-01"

    $held = @($existing.value | Where-Object {
        $_.properties.principalId -eq $principalId -and
        $_.properties.roleDefinitionId -match [regex]::Escape($FoundryUserRoleId) -and
        $_.properties.scope -eq $scope
    })

    if ($held.Count -gt 0) {
        Write-Exists "Foundry User at $label scope"
        continue
    }

    if ($PSCmdlet.ShouldProcess("$AgentName at $label scope", 'Grant Foundry User')) {
        # A fresh name each time. Idempotency comes from the check above, not from the name - ARM
        # keys an assignment by its GUID, so a duplicate would need a repeated name, and a repeated
        # name would fight any assignment someone else made at this scope.
        $assignmentName = [guid]::NewGuid().ToString()
        Invoke-Arm -Method put -What "Granting Foundry User at $label scope" `
            -Url "https://management.azure.com$scope/providers/Microsoft.Authorization/roleAssignments/$assignmentName`?api-version=2022-04-01" `
            -Body @{
                properties = @{
                    roleDefinitionId = "/subscriptions/$SubscriptionId/providers/Microsoft.Authorization/roleDefinitions/$FoundryUserRoleId"
                    principalId      = $principalId
                    principalType    = 'ServicePrincipal'
                    description      = "The $AgentName hosted agent calls the project data plane as itself."
                }
            } | Out-Null

        Write-Created "Foundry User at $label scope"
    }
}

Write-Step 'Done'
Write-Host @"
  The agent can now call the project data plane as itself - models, its own session storage, and any
  toolbox it reaches.

  Role assignments are not instant. If the agent still reports "Principal does not have access to
  API/Operation" in the next minute or so, wait before changing anything.
"@ -ForegroundColor Green
