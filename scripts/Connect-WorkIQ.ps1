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
      7. Ensure the toolbox's DEFAULT version carries the work_iq_preview tool - creating and
         promoting a new version only when it does not. The default version is what a hosted agent
         reaches through AddFoundryToolboxes.

    Idempotent, with one honest exception: a Foundry connection cannot be edited after creation. If
    one already exists its non-secret fields are compared with what this script would have created;
    a match is reported and left alone, and drift stops the run with instructions to delete the
    connection and re-run. Toolbox versions are immutable too, so a new one is created only when the
    default version does not already carry exactly the desired tool - and on an existing toolbox the
    new version is explicitly promoted to default, because only the very first one gets that free.

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
    <#  An in-memory REST call, used wherever Invoke-Json's temporary body file will not do. Two
        reasons to be here rather than there:

          - The connection body carries the client secret, and a secret must never touch disk - a
            killed process leaves the temporary file behind, plaintext credential and all.
          - Native Invoke-WebRequest bypasses az entirely, and with it Git-Bash's MSYS path
            conversion, which once quietly rewrote a "/subscriptions/..." connection id into
            "C:/Program Files/Git/subscriptions/..." inside a toolbox version.

        A 404 is an answer only when the caller says so; every other failure carries the response
        body, because these APIs put the useful sentence there. #>
    param(
        [Parameter(Mandatory)] [string] $Method,
        [Parameter(Mandatory)] [string] $Url,
        [object] $Body,
        [Parameter(Mandatory)] [string] $Resource,
        [hashtable] $ExtraHeaders = @{},
        [switch] $AllowNotFound,
        [Parameter(Mandatory)] [string] $What
    )

    $headers = @{ Authorization = "Bearer $(Get-BearerToken -Resource $Resource)" } + $ExtraHeaders
    $arguments = @{
        Method             = $Method
        Uri                = $Url
        Headers            = $headers
        SkipHttpErrorCheck = $true
    }
    if ($null -ne $Body) {
        $arguments.Body = ($Body | ConvertTo-Json -Depth 12)
        $arguments.ContentType = 'application/json'
    }

    $response = Invoke-WebRequest @arguments

    if ($response.StatusCode -eq 404 -and $AllowNotFound) { return $null }
    if ($response.StatusCode -ge 400) {
        throw "$What failed with HTTP $($response.StatusCode): $($response.Content)"
    }
    if ([string]::IsNullOrWhiteSpace($response.Content)) { return $null }
    return $response.Content | ConvertFrom-Json
}

function Test-ToolboxVersionEndpoint {
    <#  Exercises a toolbox version's own MCP endpoint before it is promoted to default: a JSON-RPC
        initialize, then tools/list. Verified live: for a user who has not consented to the Work IQ
        connection, tools/list answers HTTP 200 with a JSON-RPC error whose message embeds
        CONSENT_REQUIRED - a healthy transport waiting on a person, not a broken version. Anything
        else that is not a tools result fails the run BEFORE the default changes. #>
    param(
        [Parameter(Mandatory)] [string] $McpUrl,
        [Parameter(Mandatory)] [hashtable] $Headers
    )

    # Streamable-HTTP servers may insist the client accepts SSE even when they answer JSON.
    $probeHeaders = $Headers + @{ Accept = 'application/json, text/event-stream' }

    $init = Invoke-RestJson -Method post -Url $McpUrl -Resource 'https://ai.azure.com' -ExtraHeaders $probeHeaders -What 'MCP initialize' -Body @{
        jsonrpc = '2.0'; id = 1; method = 'initialize'
        params  = @{ protocolVersion = '2025-06-18'; capabilities = @{}; clientInfo = @{ name = 'connect-workiq-probe'; version = '1.0' } }
    }

    if (-not ($init.PSObject.Properties.Name -contains 'result')) {
        throw "MCP initialize on $McpUrl returned no result: $($init | ConvertTo-Json -Compress -Depth 8)"
    }

    $list = Invoke-RestJson -Method post -Url $McpUrl -Resource 'https://ai.azure.com' -ExtraHeaders $probeHeaders -What 'MCP tools/list' -Body @{
        jsonrpc = '2.0'; id = 2; method = 'tools/list'; params = @{}
    }

    if ($list.PSObject.Properties.Name -contains 'result') {
        return 'initialize and tools/list succeeded'
    }
    if ($list.PSObject.Properties.Name -contains 'error' -and "$($list.error.message)" -match 'CONSENT_REQUIRED') {
        return 'initialize succeeded; tools/list reports CONSENT_REQUIRED (healthy - the tool resolves per user, and this user has not consented yet)'
    }

    throw "MCP tools/list on $McpUrl failed in an unexpected way: $($list | ConvertTo-Json -Compress -Depth 8)"
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
    # Merged, never replaced: a reused application may already carry other Work IQ permissions, and
    # rewriting the resource entry to just this scope would silently revoke them. The scope is
    # appended to whatever the entry already grants.
    $others = @($app.requiredResourceAccess | Where-Object { $_.resourceAppId -ne $WorkIqAppId })
    $workIqEntry = @($app.requiredResourceAccess | Where-Object { $_.resourceAppId -eq $WorkIqAppId }) | Select-Object -First 1
    $mergedAccess = if ($workIqEntry) {
        @($workIqEntry.resourceAccess | ForEach-Object { @{ id = $_.id; type = $_.type } }) + @(@{ id = $scope.id; type = 'Scope' })
    }
    else {
        @(@{ id = $scope.id; type = 'Scope' })
    }

    Invoke-Json -Method patch -Url "$GraphBase/applications/$appObjectId" -What 'Adding the permission' -Body @{
        requiredResourceAccess = @($others + @{
            resourceAppId  = $WorkIqAppId
            resourceAccess = $mergedAccess
        })
    } | Out-Null
    Write-Created "delegated $WorkIqScopeName (existing Work IQ permissions preserved)"
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

# A grant's scope is a space-separated list, and it is matched as one: substring matching would
# read "WorkIQAgent.AskAdvanced" as consent for "WorkIQAgent.Ask".
$tenantGrant = @($grants | Where-Object { $_.consentType -eq 'AllPrincipals' }) | Select-Object -First 1
$grantScopes = if ($tenantGrant) { @(("$($tenantGrant.scope)").Split(' ', [StringSplitOptions]::RemoveEmptyEntries)) } else { @() }

if ($grantScopes -ccontains $WorkIqScopeName) {
    Write-Exists "tenant-wide consent for $WorkIqScopeName"
}
elseif ($tenantGrant) {
    if ($PSCmdlet.ShouldProcess($ApplicationName, "Add $WorkIqScopeName to the existing tenant-wide grant")) {
        # One AllPrincipals grant per client/resource pair is the model Graph enforces; a second
        # POST conflicts with the first. The existing grant is extended instead, and the scopes it
        # already carries are preserved.
        Invoke-Json -Method patch -Url "$GraphBase/oauth2PermissionGrants/$($tenantGrant.id)" -What 'Extending admin consent' -Body @{
            scope = (@($grantScopes + $WorkIqScopeName) -join ' ')
        } | Out-Null
        Write-Created "tenant-wide consent extended with $WorkIqScopeName"
    }
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

# A read that fails for any reason other than a true 404 stops the script: a 403 or a network
# failure taken for absence would walk straight into the creation branch.
$existingConnection = Invoke-RestJson -Method get -Url $connectionUrl -Resource 'https://management.azure.com' -AllowNotFound -What "Reading connection '$ConnectionName'"

if ($existingConnection) {
    # Connection fields cannot be edited after creation, so an existing connection is either
    # exactly what this script would have created - reported and left alone - or drift, which
    # stops the run. The comparison covers the observable fields that matter to behaviour;
    # credentials are redacted by ARM (the record returns "credentials": null), so the client id
    # and secret genuinely cannot be checked - which is why the report says "selected observable
    # fields", not "everything".
    #
    # Deliberately NOT compared, verified against a live record: `group` and `isSharedToAll` are
    # rewritten by the service (this script sends ServicesAndApps/true, the record stores
    # AzureAI/false), so comparing them would report drift on every healthy connection.
    $props = $existingConnection.properties
    $observed = @{}
    foreach ($name in @('authType', 'category', 'target', 'tokenUrl', 'authorizationUrl', 'refreshUrl')) {
        $observed[$name] = if ($props.PSObject.Properties.Name -contains $name) { "$($props.$name)" } else { '' }
    }
    $metadata = if ($props.PSObject.Properties.Name -contains 'metadata' -and $props.metadata) { $props.metadata } else { $null }
    $observed['metadata.type'] = if ($metadata -and $metadata.PSObject.Properties.Name -contains 'type') { "$($metadata.type)" } else { '' }
    $observed['metadata.oAuthProvider'] = if ($metadata -and $metadata.PSObject.Properties.Name -contains 'oAuthProvider') { "$($metadata.oAuthProvider)" } else { '' }

    # Scopes are a set: order must not matter, and an extra or overly broad scope must.
    $observedScopes = if ($props.PSObject.Properties.Name -contains 'scopes' -and $props.scopes) { @($props.scopes) } else { @() }
    $desiredScopes = @('offline_access', "api://workiq.svc.cloud.microsoft/$WorkIqScopeName")
    $observed['scopes'] = (@($observedScopes | Sort-Object) -join ' ')

    $desired = [ordered]@{
        'authType'               = 'OAuth2'
        'category'               = 'RemoteA2A'
        'target'                 = $WorkIqA2AEndpoint
        'tokenUrl'               = "https://login.microsoftonline.com/$tenantId/oauth2/v2.0/token"
        'authorizationUrl'       = "https://login.microsoftonline.com/$tenantId/oauth2/v2.0/authorize"
        'refreshUrl'             = "https://login.microsoftonline.com/$tenantId/oauth2/v2.0/token"
        'metadata.type'          = 'work_iq_preview'
        'metadata.oAuthProvider' = 'custom'
        'scopes'                 = (@($desiredScopes | Sort-Object) -join ' ')
    }

    $drifted = @($desired.Keys | Where-Object { -not $observed[$_].Equals($desired[$_], [StringComparison]::OrdinalIgnoreCase) })

    # A connection carrying a recorded error is not healthy however well its fields compare.
    if ($props.PSObject.Properties.Name -contains 'error' -and $props.error) {
        $drifted += 'error'
        $observed['error'] = "$($props.error | ConvertTo-Json -Compress -Depth 4)"
        $desired['error'] = '(none)'
    }

    if ($drifted.Count -gt 0) {
        $detail = ($drifted | ForEach-Object { "$($_): expected '$($desired[$_])', found '$($observed[$_])'" }) -join [Environment]::NewLine
        throw @"
Connection '$ConnectionName' exists but does not match what this script creates, and connection
fields cannot be edited after creation. Delete it and re-run:

  az rest --method delete --url "$connectionUrl"

Drift:
$detail
"@
    }

    Write-Exists "connection '$ConnectionName' (selected observable fields match; credentials are not readable back)"
    $connection = $existingConnection
}
elseif ($PSCmdlet.ShouldProcess($ConnectionName, 'Create the Work IQ connection')) {
    # The secret is created here and handed straight to the connection, fully in memory: the
    # connection body goes through Invoke-RestJson precisely so no temporary file ever carries it.
    # The connection is the only place the secret needs to exist.
    $secret = Invoke-Json -Method post -Url "$GraphBase/applications/$appObjectId/addPassword" -What 'Creating a client secret' -Body @{
        passwordCredential = @{ displayName = "foundry-$ConnectionName" }
    }

    try {
        $connection = Invoke-RestJson -Method put -Url $connectionUrl -Resource 'https://management.azure.com' -What 'Creating the connection' -Body @{
            properties = @{
                authType         = 'OAuth2'
                group            = 'ServicesAndApps'
                category         = 'RemoteA2A'
                target           = $WorkIqA2AEndpoint
                isSharedToAll    = $true
                TokenUrl         = "https://login.microsoftonline.com/$tenantId/oauth2/v2.0/token"
                AuthorizationUrl = "https://login.microsoftonline.com/$tenantId/oauth2/v2.0/authorize"
                RefreshUrl       = "https://login.microsoftonline.com/$tenantId/oauth2/v2.0/token"
                # Order matches what the portal writes. Probably cosmetic, kept identical so a future
                # diff against a portal-made connection shows nothing and means it.
                Scopes           = @('offline_access', "api://workiq.svc.cloud.microsoft/$WorkIqScopeName")
                Credentials      = @{ ClientId = $appId; ClientSecret = $secret.secretText }
                # This is the field that decides whether the connection works, and the documented REST
                # example gets it wrong: it says @{ ApiType = 'Azure' }, which produces a connection whose
                # every visible property is correct and whose API Hub connector is never provisioned. The
                # first call then fails with "ApiHub CreateConnection failed ... StatusCode=404 Not Found"
                # and an HTML error page, naming nothing useful. `type` is what selects the connector.
                # Established by creating one through the portal and diffing the two records.
                metadata         = @{ oAuthProvider = 'custom'; type = 'work_iq_preview' }
            }
        }

        Write-Created "connection '$ConnectionName'"
    }
    catch {
        $creationError = $_

        # A failed PUT is ambiguous: Azure may have accepted it while the client timed out or
        # choked on the response. Deleting the password on that evidence alone would invalidate a
        # connection that actually exists and holds it - so the truth is read back first.
        $check = $null
        try {
            $check = Invoke-RestJson -Method get -Url $connectionUrl -Resource 'https://management.azure.com' -AllowNotFound -What "Re-reading connection '$ConnectionName' after the failed create"
        }
        catch {
            Write-Warn "Connection creation failed and the re-read also failed. The minted secret (keyId $($secret.keyId)) was NOT removed, because the connection may exist and hold it. Reconcile manually."
            throw $creationError
        }

        if ($null -eq $check) {
            # Truly absent: the password authenticates nothing and must not outlive the failure as
            # a live credential nobody knows exists.
            try {
                Invoke-Json -Method post -Url "$GraphBase/applications/$appObjectId/removePassword" -What 'Removing the unused client secret' -Body @{
                    keyId = $secret.keyId
                } | Out-Null
                Write-Warn 'Connection creation failed; the client secret minted for it was removed again.'
            }
            catch {
                Write-Warn "Connection creation failed AND the freshly minted secret could not be removed. Remove it manually: keyId $($secret.keyId) on application $appObjectId."
            }
            throw $creationError
        }

        $checkMetadata = if ($check.properties.PSObject.Properties.Name -contains 'metadata' -and $check.properties.metadata) { $check.properties.metadata } else { $null }
        $checkType = if ($checkMetadata -and $checkMetadata.PSObject.Properties.Name -contains 'type') { "$($checkMetadata.type)" } else { '' }

        if ($checkType -eq 'work_iq_preview' -and "$($check.properties.target)" -eq $WorkIqA2AEndpoint) {
            # The PUT landed; only the response was lost. The connection holds the secret, so the
            # secret stays, and the run continues on the adopted record.
            Write-Warn "The create call reported a failure but the connection exists and matches; adopting it. (Original error: $($creationError.Exception.Message))"
            Write-Exists "connection '$ConnectionName' (adopted after an ambiguous create)"
            $connection = $check
        }
        else {
            # Landed wrong: a half-made connection is worse than none. Remove it, then the secret.
            try {
                Invoke-RestJson -Method delete -Url $connectionUrl -Resource 'https://management.azure.com' -What "Deleting the malformed connection '$ConnectionName'" | Out-Null
                Invoke-Json -Method post -Url "$GraphBase/applications/$appObjectId/removePassword" -What 'Removing the unused client secret' -Body @{
                    keyId = $secret.keyId
                } | Out-Null
                Write-Warn 'The malformed connection and its client secret were removed.'
            }
            catch {
                Write-Warn "Cleanup after the failed create was itself incomplete. Check connection '$ConnectionName' and secret keyId $($secret.keyId) manually."
            }
            throw $creationError
        }
    }
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
$foundryHeaders = @{ 'Foundry-Features' = 'Toolboxes=V1Preview' }
$toolboxUrl = "$projectEndpoint/toolboxes/$ToolboxName`?api-version=v1"

# Versions are immutable and the DEFAULT version is what AddFoundryToolboxes resolves, so the only
# honest comparison is against the default's tools. Creating a version per run - which this script
# once did - grows an immutable pile the runtime never looks at: only a toolbox's very first
# version becomes the default for free, and this project accumulated versions 1-5 with the default
# still at 1 before that was understood.
$toolbox = Invoke-RestJson -Method get -Url $toolboxUrl -Resource 'https://ai.azure.com' -ExtraHeaders $foundryHeaders -AllowNotFound -What "Reading toolbox '$ToolboxName'"

$defaultMatches = $false
if ($toolbox) {
    $defaultVersion = "$($toolbox.default_version)"
    $currentDefault = Invoke-RestJson -Method get `
        -Url "$projectEndpoint/toolboxes/$ToolboxName/versions/$defaultVersion`?api-version=v1" `
        -Resource 'https://ai.azure.com' -ExtraHeaders $foundryHeaders `
        -What "Reading toolbox version $defaultVersion"

    # Exactly one tool of exactly the desired shape. A duplicated tool is drift too - one live
    # version of this very toolbox carries work_iq_preview twice.
    $tools = @($currentDefault.tools)
    $defaultMatches = $tools.Count -eq 1 -and
        "$($tools[0].type)" -eq 'work_iq_preview' -and
        "$($tools[0].project_connection_id)" -eq $connectionId
}

if ($defaultMatches) {
    Write-Exists "default version $defaultVersion already carries work_iq_preview -> '$ConnectionName'"
    Write-Note "MCP endpoint: $projectEndpoint/toolboxes/$ToolboxName/versions/$defaultVersion/mcp?api-version=v1"
}
elseif ($PSCmdlet.ShouldProcess($ToolboxName, 'Ensure a verified toolbox version carries the Work IQ tool and is the default')) {
    # A staged version that already matches is reused rather than duplicated: versions are
    # immutable, so every avoidable create is permanent clutter (this toolbox once accumulated
    # five, one with the tool doubled).
    $version = $null
    if ($toolbox) {
        $allVersions = @((Invoke-RestJson -Method get `
            -Url "$projectEndpoint/toolboxes/$ToolboxName/versions?api-version=v1" `
            -Resource 'https://ai.azure.com' -ExtraHeaders $foundryHeaders `
            -What 'Listing toolbox versions').data)

        $version = $allVersions |
            Where-Object {
                $candidateTools = @($_.tools)
                $candidateTools.Count -eq 1 -and
                    "$($candidateTools[0].type)" -eq 'work_iq_preview' -and
                    "$($candidateTools[0].project_connection_id)" -eq $connectionId
            } |
            Sort-Object { [int]$_.version } -Descending |
            Select-Object -First 1
    }

    if ($version) {
        Write-Exists "version $($version.version) already carries the desired tool; reusing it rather than creating a duplicate"
    }
    else {
        $version = Invoke-RestJson -Method post `
            -Url "$projectEndpoint/toolboxes/$ToolboxName/versions?api-version=v1" `
            -Resource 'https://ai.azure.com' -ExtraHeaders $foundryHeaders `
            -What 'Creating the toolbox version' `
            -Body @{
                # "As the signed-in user", deliberately not "read as": WorkIQAgent.Ask is not a
                # read-only permission - it can act on Microsoft 365 content too. The agent composed on
                # this toolbox is instructed to use it for evidence retrieval only, and that boundary
                # lives in the agent's instructions, not in this grant.
                description = 'Work IQ: Microsoft 365 context, asked as the signed-in user. The delegated permission can act as well as read; agents on this toolbox are constrained to evidence retrieval by instruction.'
                tools       = @(@{ type = 'work_iq_preview'; project_connection_id = $connectionId })
            }

        Write-Created "toolbox '$ToolboxName' version $($version.version)"
    }

    # Exercised BEFORE it becomes the default: a version that cannot even initialize must fail here,
    # while the old default is still serving the runtime.
    $mcpUrl = "$projectEndpoint/toolboxes/$ToolboxName/versions/$($version.version)/mcp?api-version=v1"
    $probe = Test-ToolboxVersionEndpoint -McpUrl $mcpUrl -Headers $foundryHeaders
    Write-Note "MCP probe: $probe"

    if ($toolbox) {
        # Promotion is explicit on an existing toolbox. If this PATCH shape stops being accepted,
        # failing loudly here beats leaving a correct version the runtime will never use.
        Invoke-RestJson -Method patch -Url $toolboxUrl `
            -Resource 'https://ai.azure.com' -ExtraHeaders $foundryHeaders `
            -What 'Promoting the version to default' `
            -Body @{ default_version = "$($version.version)" } | Out-Null
    }

    # Verified, not assumed - for the first version of a new toolbox this also confirms the
    # platform really did make it the default.
    $after = Invoke-RestJson -Method get -Url $toolboxUrl -Resource 'https://ai.azure.com' -ExtraHeaders $foundryHeaders -What "Re-reading toolbox '$ToolboxName'"
    if ("$($after.default_version)" -ne "$($version.version)") {
        throw "Version $($version.version) is verified but the toolbox default is still $($after.default_version). The hosted agent would keep using the old default; promote it manually or delete the toolbox and re-run."
    }

    Write-Created "default_version -> $($version.version)"
    Write-Note "MCP endpoint: $mcpUrl"
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

  One boundary to state honestly: WorkIQAgent.Ask is NOT read-only - it can act on Microsoft 365
  content as well as read it. The demo agent is constrained to evidence retrieval by its
  instructions, which is a behavioural boundary, not a permission one. An agent that must be
  UNABLE to write needs a narrower permission when Microsoft ships one, or an approval-gated
  composition around this tool.

  Two things that are not this script's to give you:

    - Work IQ API calls need usage-based billing with Copilot Credits. Without it, calls return 403.
    - The agent's runtime identity needs the Foundry User role on the project, or calls fail with
      "Principal does not have access to API/Operation".

  Next: point the hosted agent at the toolbox with AddFoundryToolboxes("$ToolboxName").
"@ -ForegroundColor Green
