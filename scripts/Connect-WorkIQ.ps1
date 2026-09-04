#Requires -Version 7.0

<#
.SYNOPSIS
    Wires Work IQ into the Foundry project end to end: Entra app, admin consent, connection, toolbox.

.DESCRIPTION
    Work IQ is the Microsoft 365 intelligence layer. Connected this way, the agent asks it questions
    **as the signed-in user** - Foundry performs the OAuth on-behalf-of exchange, so the agent never
    holds a user token, and Microsoft 365 permissions and sensitivity labels are enforced by M365
    rather than by the agent. Application-only access is not supported, which is the point: an agent
    that could read everyone's mail would be a worse demonstration of governance, not a better one.

    Every step here is an API call. There is no portal step.

      1. Provision the Work IQ service principal (appId fdcc1f02-fc51-4226-8753-f668596af7f7).
         Without it, WorkIQAgent.Ask cannot be granted - the permission simply will not be findable.
      2. Register a single-tenant confidential client app. This is the app the admin authorises;
         it is what makes "which application may read M365 data through Work IQ" an explicit decision.
      3. Add the delegated WorkIQAgent.Ask permission and grant tenant-wide admin consent.
      4. Mint a client secret, and hand it straight to the connection without printing it.
      5. Create the project connection (RemoteA2A, OAuth2) - note that Work IQ is itself an A2A agent.
      6. Read the OAuth redirect URL the connection returns and add it to the app registration. This
         ordering is forced: the URL does not exist until the connection does.
      7. Create a toolbox version carrying the work_iq_preview tool, which is what a hosted agent
         reaches through AddFoundryToolboxes.

    Idempotent, with one honest exception: a Foundry connection cannot be edited after creation. If
    one already exists this script leaves it alone rather than pretending to update it. To change it,
    delete it and re-run.

.PARAMETER Environment
    Environment whose project to wire up.

.PARAMETER ConnectionName
    Name of the Foundry connection to create.

.PARAMETER ToolboxName
    Name of the toolbox to create a version of. The hosted agent looks this up by name.

.PARAMETER ApplicationName
    Display name of the Entra app registration to create or reuse.

.PARAMETER SubscriptionId
    Subscription holding the Foundry account. Defaults to the current az account.

.PARAMETER ResourceGroupName
    Resource group holding the Foundry account. Defaults to rg-caesarea-<environment>.

.EXAMPLE
    ./scripts/Connect-WorkIQ.ps1 -WhatIf

.EXAMPLE
    ./scripts/Connect-WorkIQ.ps1 -Environment dev
#>

[CmdletBinding(SupportsShouldProcess)]
param(
    [ValidateSet('dev', 'prod')]
    [string] $Environment = 'dev',
    [string] $ConnectionName = 'caesarea-workiq',
    [string] $ToolboxName = 'caesarea-workiq',
    [string] $ApplicationName,
    [string] $SubscriptionId,
    [string] $ResourceGroupName,
    # Mint a fresh client secret and print it, for pasting into the Foundry portal's connection
    # dialog. Off by default: a secret that is printed is a secret that ends up in scrollback, a
    # terminal log and someone's screen recording. Use it only when the portal path is needed.
    [switch] $ShowClientSecret
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# The Work IQ resource application. Fixed by Microsoft, not by this tenant.
$WorkIqAppId = 'fdcc1f02-fc51-4226-8753-f668596af7f7'
$WorkIqScopeName = 'WorkIQAgent.Ask'
$WorkIqA2AEndpoint = 'https://workiq.svc.cloud.microsoft/a2a/'
$GraphBase = 'https://graph.microsoft.com/v1.0'

if (-not $ApplicationName) { $ApplicationName = "caesarea-workiq-client-$Environment" }
if (-not $ResourceGroupName) { $ResourceGroupName = "rg-caesarea-$Environment" }

function Write-Step { param([string] $Message) Write-Host "`n=== $Message" -ForegroundColor Cyan }
function Write-Exists { param([string] $Message) Write-Host "  [exists] $Message" -ForegroundColor DarkGray }
function Write-Created { param([string] $Message) Write-Host "  [created] $Message" -ForegroundColor Green }
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

function Invoke-Json {
    <#  A REST call through `az rest`, with any body written to a file. Inline JSON through az on
        Windows means fighting two layers of quoting, and it fails by silent truncation. #>
    param(
        [Parameter(Mandatory)] [string] $Method,
        [Parameter(Mandatory)] [string] $Url,
        [object] $Body,
        [string[]] $Headers = @('Content-Type=application/json'),
        # az derives the token audience from the URL for ARM and Graph. It cannot for the Foundry
        # data plane, and the failure is a confusing 401 about "invalid subscription key" rather
        # than anything about audiences - so data-plane calls state it.
        [string] $Resource,
        [Parameter(Mandatory)] [string] $What
    )

    $resourceArgs = if ($Resource) { @('--resource', $Resource) } else { @() }

    if ($null -eq $Body) {
        $output = Invoke-Checked { az rest --method $Method --url $Url --headers @Headers @resourceArgs --output json } $What
    }
    else {
        $file = New-TemporaryFile
        try {
            ($Body | ConvertTo-Json -Depth 12) | Set-Content -Path $file -Encoding utf8
            $output = Invoke-Checked {
                az rest --method $Method --url $Url --headers @Headers @resourceArgs --body "@$file" --output json
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

if (-not (Get-Command az -ErrorAction SilentlyContinue)) { throw 'az is not on PATH.' }

$account = (Invoke-Checked { az account show --output json } 'Reading the current Azure account') | ConvertFrom-Json
if (-not $SubscriptionId) { $SubscriptionId = $account.id }
$tenantId = $account.tenantId

Write-Note "Tenant:        $tenantId"
Write-Note "Subscription:  $SubscriptionId"
Write-Note "Resource group: $ResourceGroupName"
Write-Note "Application:   $ApplicationName"
Write-Note "Connection:    $ConnectionName"
Write-Note "Toolbox:       $ToolboxName"

# The Foundry account and project names, read from the resource group rather than assumed.
$accounts = @(@(Invoke-Checked {
    az cognitiveservices account list --resource-group $ResourceGroupName --subscription $SubscriptionId --query "[?kind=='AIServices'].name" --output tsv
} 'Listing Foundry accounts') | ForEach-Object { "$_".Trim() } | Where-Object { $_ })

if ($accounts.Count -ne 1) {
    throw "Expected exactly one AIServices account in '$ResourceGroupName'; found $($accounts.Count). Deploy the platform first."
}
$accountName = $accounts[0]
$projectName = "caesarea-$Environment"

Write-Note "Foundry:       $accountName / $projectName"

# ---------------------------------------------------------------------------------------------
# 1. The Work IQ service principal. Without it, the permission cannot even be found.
# ---------------------------------------------------------------------------------------------

Write-Step 'Work IQ service principal'

$workIqSps = @((Invoke-Json -Method get -Url "$GraphBase/servicePrincipals?`$filter=appId eq '$WorkIqAppId'" -What 'Looking for the Work IQ service principal').value)

if ($workIqSps.Count -gt 0) {
    $workIqSp = $workIqSps[0]
    Write-Exists "Work IQ ($($workIqSp.id))"
}
elseif ($PSCmdlet.ShouldProcess('Work IQ', 'Provision the service principal in this tenant')) {
    # Requires Global Administrator. Provisioning it is what makes WorkIQAgent.Ask grantable.
    $workIqSp = Invoke-Json -Method post -Url "$GraphBase/servicePrincipals" -What 'Provisioning the Work IQ service principal' -Body @{
        appId = $WorkIqAppId
    }
    Write-Created "Work IQ ($($workIqSp.id))"
}
else {
    Write-Note 'Skipped; nothing below can be previewed without it.'
    return
}

$scope = $workIqSp.oauth2PermissionScopes | Where-Object { $_.value -eq $WorkIqScopeName } | Select-Object -First 1
if (-not $scope) {
    throw "The Work IQ service principal exposes no '$WorkIqScopeName' delegated scope. Its API surface may have changed."
}
Write-Note "Scope:         $WorkIqScopeName ($($scope.id))"

# ---------------------------------------------------------------------------------------------
# 2-3. The client app, its permission, and tenant-wide consent.
# ---------------------------------------------------------------------------------------------

Write-Step "Application registration '$ApplicationName'"

$existing = @(@(Invoke-Checked {
    az ad app list --display-name $ApplicationName --query "[].{appId:appId,id:id}" --output json
} "Listing applications named '$ApplicationName'") | ConvertFrom-Json)

if ($existing.Count -gt 1) {
    throw "Found $($existing.Count) applications named '$ApplicationName'. Entra permits duplicate display names; delete the extras or rename them."
}

if ($existing.Count -eq 1) {
    $appId = $existing[0].appId
    $appObjectId = $existing[0].id
    Write-Exists "application ($appId)"
}
elseif ($PSCmdlet.ShouldProcess($ApplicationName, 'Create a single-tenant Entra application')) {
    $created = Invoke-Json -Method post -Url "$GraphBase/applications" -What "Creating '$ApplicationName'" -Body @{
        displayName    = $ApplicationName
        # Single tenant, per the Work IQ setup guidance.
        signInAudience = 'AzureADMyOrg'
        requiredResourceAccess = @(@{
            resourceAppId  = $WorkIqAppId
            resourceAccess = @(@{ id = $scope.id; type = 'Scope' })
        })
    }
    $appId = $created.appId
    $appObjectId = $created.id
    Write-Created "application ($appId) with delegated $WorkIqScopeName"
    Start-Sleep -Seconds 10
}
else {
    Write-Note 'Skipped.'
    return
}

# The permission, when reusing an existing app that may not carry it yet.
$app = Invoke-Json -Method get -Url "$GraphBase/applications/$appObjectId" -What 'Reading the application'
$hasPermission = $false
foreach ($resource in @($app.requiredResourceAccess)) {
    if ($resource.resourceAppId -eq $WorkIqAppId -and @($resource.resourceAccess).Where({ $_.id -eq $scope.id }).Count -gt 0) {
        $hasPermission = $true
    }
}

if ($hasPermission) {
    Write-Exists "delegated $WorkIqScopeName on the application"
}
elseif ($PSCmdlet.ShouldProcess($ApplicationName, "Add delegated $WorkIqScopeName")) {
    $others = @($app.requiredResourceAccess | Where-Object { $_.resourceAppId -ne $WorkIqAppId })
    Invoke-Json -Method patch -Url "$GraphBase/applications/$appObjectId" -What 'Adding the permission' -Body @{
        requiredResourceAccess = @($others + @{
            resourceAppId  = $WorkIqAppId
            resourceAccess = @(@{ id = $scope.id; type = 'Scope' })
        })
    } | Out-Null
    Write-Created "delegated $WorkIqScopeName"
}

Write-Step 'Admin consent'

# The app's own service principal must exist before consent can be recorded against it.
$clientSps = @((Invoke-Json -Method get -Url "$GraphBase/servicePrincipals?`$filter=appId eq '$appId'" -What 'Looking for the client service principal').value)
if ($clientSps.Count -gt 0) {
    $clientSp = $clientSps[0]
    Write-Exists "service principal ($($clientSp.id))"
}
elseif ($PSCmdlet.ShouldProcess($ApplicationName, 'Create its service principal')) {
    $clientSp = Invoke-Json -Method post -Url "$GraphBase/servicePrincipals" -What 'Creating the client service principal' -Body @{ appId = $appId }
    Write-Created "service principal ($($clientSp.id))"
    Start-Sleep -Seconds 5
}

$grants = @((Invoke-Json -Method get -Url "$GraphBase/oauth2PermissionGrants?`$filter=clientId eq '$($clientSp.id)' and resourceId eq '$($workIqSp.id)'" -What 'Reading existing consent').value)
$consented = @($grants | Where-Object { $_.scope -match [regex]::Escape($WorkIqScopeName) })

if ($consented.Count -gt 0) {
    Write-Exists "tenant-wide consent for $WorkIqScopeName"
}
elseif ($PSCmdlet.ShouldProcess($ApplicationName, "Grant tenant-wide admin consent for $WorkIqScopeName")) {
    # AllPrincipals: consent granted once for the organisation. Requires Global Administrator.
    Invoke-Json -Method post -Url "$GraphBase/oauth2PermissionGrants" -What 'Granting admin consent' -Body @{
        clientId    = $clientSp.id
        consentType = 'AllPrincipals'
        resourceId  = $workIqSp.id
        scope       = $WorkIqScopeName
    } | Out-Null
    Write-Created "tenant-wide consent for $WorkIqScopeName"
}

# ---------------------------------------------------------------------------------------------
# 4-6. The connection, its secret, and the redirect URI it hands back.
# ---------------------------------------------------------------------------------------------

Write-Step "Foundry connection '$ConnectionName'"

$connectionUrl = "https://management.azure.com/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroupName/providers/Microsoft.CognitiveServices/accounts/$accountName/projects/$projectName/connections/$ConnectionName`?api-version=2025-04-01-preview"

$existingConnection = az rest --method get --url $connectionUrl --output json 2>$null
if ($LASTEXITCODE -eq 0 -and $existingConnection) {
    $global:LASTEXITCODE = 0
    # Connection fields cannot be edited after creation. Reporting and leaving it alone beats
    # pretending to update something the API will not change.
    Write-Exists "connection '$ConnectionName' (fields are immutable; delete it to change them)"
    $connection = $existingConnection | ConvertFrom-Json
}
elseif ($PSCmdlet.ShouldProcess($ConnectionName, 'Create the Work IQ connection')) {
    $global:LASTEXITCODE = 0

    # The secret is created here and handed straight to the connection. It is never written to a
    # file, printed, or returned - the connection is the only place it needs to exist.
    $secret = Invoke-Json -Method post -Url "$GraphBase/applications/$appObjectId/addPassword" -What 'Creating a client secret' -Body @{
        passwordCredential = @{ displayName = "foundry-$ConnectionName" }
    }

    $connection = Invoke-Json -Method put -Url $connectionUrl -What 'Creating the connection' -Body @{
        properties = @{
            authType         = 'OAuth2'
            group            = 'ServicesAndApps'
            category         = 'RemoteA2A'
            target           = $WorkIqA2AEndpoint
            isSharedToAll    = $true
            TokenUrl         = "https://login.microsoftonline.com/$tenantId/oauth2/v2.0/token"
            AuthorizationUrl = "https://login.microsoftonline.com/$tenantId/oauth2/v2.0/authorize"
            RefreshUrl       = "https://login.microsoftonline.com/$tenantId/oauth2/v2.0/token"
            Scopes           = @("api://workiq.svc.cloud.microsoft/$WorkIqScopeName", 'offline_access')
            Credentials      = @{ ClientId = $appId; ClientSecret = $secret.secretText }
            metadata         = @{ ApiType = 'Azure' }
        }
    }

    Write-Created "connection '$ConnectionName'"
}

Write-Step 'OAuth redirect URI'

$redirectUrl = $null
if ($connection -and $connection.PSObject.Properties.Name -contains 'properties') {
    $props = $connection.properties
    foreach ($name in @('redirectUrl', 'oauthRedirectUrl', 'OAuthRedirectUrl', 'redirectUri')) {
        if ($props.PSObject.Properties.Name -contains $name -and $props.$name) { $redirectUrl = $props.$name; break }
    }
}

if (-not $redirectUrl) {
    Write-Warn 'The connection returned no OAuth redirect URL. Read it from the connection in the portal and add it'
    Write-Warn "as a Web redirect URI on '$ApplicationName' - the OAuth flow fails without it."
}
else {
    Write-Note "Redirect: $redirectUrl"
    $current = @($app.web.redirectUris)
    if ($current -contains $redirectUrl) {
        Write-Exists 'redirect URI already registered'
    }
    elseif ($PSCmdlet.ShouldProcess($ApplicationName, 'Add the OAuth redirect URI')) {
        Invoke-Json -Method patch -Url "$GraphBase/applications/$appObjectId" -What 'Adding the redirect URI' -Body @{
            web = @{ redirectUris = @($current + $redirectUrl) }
        } | Out-Null
        Write-Created 'redirect URI'
    }
}

# ---------------------------------------------------------------------------------------------
# 7. The toolbox version. This is what a hosted agent reaches by name.
# ---------------------------------------------------------------------------------------------

Write-Step "Toolbox '$ToolboxName'"

$projectEndpoint = "https://$accountName.services.ai.azure.com/api/projects/$projectName"
$connectionId = "/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroupName/providers/Microsoft.CognitiveServices/accounts/$accountName/projects/$projectName/connections/$ConnectionName"

if ($PSCmdlet.ShouldProcess($ToolboxName, 'Create a toolbox version carrying the Work IQ tool')) {
    $version = Invoke-Json -Method post `
        -Url "$projectEndpoint/toolboxes/$ToolboxName/versions?api-version=v1" `
        -Headers @('Content-Type=application/json', 'Foundry-Features=Toolboxes=V1Preview') `
        -Resource 'https://ai.azure.com' `
        -What 'Creating the toolbox version' `
        -Body @{
            description = 'Work IQ: Microsoft 365 context, read as the signed-in user.'
            tools       = @(@{ type = 'work_iq_preview'; project_connection_id = $connectionId })
        }

    Write-Created "toolbox '$ToolboxName' version $($version.version)"
    Write-Note "MCP endpoint: $projectEndpoint/toolboxes/$ToolboxName/versions/$($version.version)/mcp?api-version=v1"
}

if ($ShowClientSecret) {
    Write-Step 'Client secret'

    if ($PSCmdlet.ShouldProcess($ApplicationName, 'Mint a new client secret and print it')) {
        $shown = Invoke-Json -Method post -Url "$GraphBase/applications/$appObjectId/addPassword" -What 'Creating a client secret' -Body @{
            passwordCredential = @{ displayName = "portal-$ConnectionName" }
        }

        Write-Host ''
        Write-Host "  Client ID:     $appId" -ForegroundColor Yellow
        Write-Host "  Client secret: $($shown.secretText)" -ForegroundColor Yellow
        Write-Host ''
        Write-Warn 'Shown once and never stored. Paste it into the portal now; if you lose it, re-run'
        Write-Warn 'with -ShowClientSecret to mint another rather than trying to recover this one.'
        Write-Warn 'Delete unused secrets afterwards - every one that outlives its use is a live credential.'
    }
}

Write-Step 'Done'
Write-Host @"
  Work IQ is connected as an A2A peer, reached through a toolbox.

  What this buys, and it is worth saying precisely: the agent asks Work IQ questions **as the
  signed-in user**. Foundry performs the on-behalf-of exchange, so the agent never holds a user
  token, and Microsoft 365 decides what comes back - permissions, sensitivity labels and all. An
  admin controls which application may do this at all, by consenting to one named delegated
  permission for one named app.

  Two things that are not this script's to give you:

    - Work IQ API calls need usage-based billing with Copilot Credits. Without it, calls return 403.
    - The agent's runtime identity needs the Foundry User role on the project, or calls fail with
      "Principal does not have access to API/Operation".

  Next: point the hosted agent at the toolbox with AddFoundryToolboxes("$ToolboxName").
"@ -ForegroundColor Green
