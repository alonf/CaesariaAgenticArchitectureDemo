#Requires -Version 7.0

<#
.SYNOPSIS
    Puts the hosted Caesarea Operations Agent into a Teams channel - as an honestly labeled relay
    run by the presenter.

.DESCRIPTION
    Watches a Teams channel for new top-level messages, forwards each one to the Foundry-hosted
    agent, and posts the answer back as a reply. The native path - publishing the agent to
    Microsoft 365 through the Agent 365 tooling so it appears as itself - is the follow-up
    documented in docs/prompts/13-agent365.md; this relay is the demonstrable-today bridge, and it
    says what it is in every reply it posts.

    THE IDENTITY BOUNDARY, stated up front because it is the interesting part: the relay runs as
    the signed-in presenter, so EVERY question is asked of the agent as that person - including a
    question typed by someone else in the channel. Work IQ evidence comes from the presenter's
    Microsoft 365, whoever asked. That is what distinguishes a relay from a published agent, and
    the demo should say so out loud. (It is also why the replies carry "relayed as <user>".)

    Reading channel messages needs the ChannelMessage.Read.All delegated permission, which the
    Azure CLI does not carry by default. Run once with -GrantReadConsent (as an admin) to add it as
    a PRINCIPAL-scoped grant - this signed-in user only, never tenant-wide - merged into any grant
    the user already has, existing scopes preserved.

.PARAMETER TeamName
    Display name of the team.

.PARAMETER ChannelName
    Display name of the channel inside it.

.PARAMETER Environment
    Environment whose FOUNDRY_PROJECT_ENDPOINT to read, when one is not supplied directly.

.PARAMETER ProjectEndpoint
    Foundry project endpoint. Defaults to the environment's FOUNDRY_PROJECT_ENDPOINT variable.

.PARAMETER AgentName
    The hosted agent that answers.

.PARAMETER Repository
    owner/name of the GitHub repository, used only to read the endpoint default.

.PARAMETER PollSeconds
    Seconds between channel polls.

.PARAMETER LookbackMinutes
    Also answer top-level messages posted this many minutes BEFORE the relay started. Zero by
    default: history is not a queue, and answering last week's messages on startup would spam the
    channel.

.PARAMETER Once
    Poll a single time, answer what is found, and exit. For rehearsal and testing.

.PARAMETER GrantReadConsent
    Grant this user's Azure CLI the ChannelMessage.Read.All delegated permission (admin action,
    principal-scoped, idempotent) and exit. Run once per tenant per user.

.EXAMPLE
    ./scripts/Start-TeamsOperatorRelay.ps1 -GrantReadConsent

.EXAMPLE
    ./scripts/Start-TeamsOperatorRelay.ps1

.EXAMPLE
    ./scripts/Start-TeamsOperatorRelay.ps1 -Once -LookbackMinutes 5
#>

[CmdletBinding(SupportsShouldProcess)]
param(
    [string] $TeamName = "Alon's Demos",
    [string] $ChannelName = 'Demo',
    [ValidateSet('dev', 'prod')]
    [string] $Environment = 'dev',
    [string] $ProjectEndpoint,
    [string] $AgentName = 'caesarea-operations',
    [string] $Repository,
    [ValidateRange(2, 300)]
    [int] $PollSeconds = 5,
    [ValidateRange(0, 1440)]
    [int] $LookbackMinutes = 0,
    [switch] $Once,
    [switch] $GrantReadConsent
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$GraphBase = 'https://graph.microsoft.com/v1.0'
$AzureCliAppId = '04b07795-8ddb-461a-bbee-02f9e1bf7b46'
$GraphResourceAppId = '00000003-0000-0000-c000-000000000000'
$ReadScope = 'ChannelMessage.Read.All'

function Write-Step { param([string] $Message) Write-Host "`n=== $Message" -ForegroundColor Cyan }
function Write-Exists { param([string] $Message) Write-Host "  [exists] $Message" -ForegroundColor DarkGray }
function Write-Created { param([string] $Message) Write-Host "  [done] $Message" -ForegroundColor Green }
function Write-Note { param([string] $Message) Write-Host "  $Message" -ForegroundColor DarkGray }
function Write-Warn { param([string] $Message) Write-Host "  $Message" -ForegroundColor Yellow }
function Write-Beat { param([string] $Message) Write-Host "  $Message" -ForegroundColor Green }

function Invoke-Checked {
    param([Parameter(Mandatory)] [scriptblock] $Command, [Parameter(Mandatory)] [string] $What)

    $output = & $Command 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "$What failed (exit $LASTEXITCODE): $($output -join [Environment]::NewLine)"
    }
    return $output
}

function Get-GraphToken {
    <#  The read permission may have been granted minutes ago, and az serves .default tokens from
        cache until they expire - so the read path asks for the scope EXPLICITLY, which is its own
        cache entry and therefore fresh. Send and everything else ride the ordinary token. #>
    param([switch] $ForRead)

    if ($ForRead) {
        $token = az account get-access-token --scope "https://graph.microsoft.com/$ReadScope" --query accessToken --output tsv 2>$null
        if ($LASTEXITCODE -eq 0 -and $token) { return "$token".Trim() }
        $global:LASTEXITCODE = 0
    }
    return (Invoke-Checked { az account get-access-token --resource https://graph.microsoft.com --query accessToken --output tsv } 'Getting a Graph token').Trim()
}

function Invoke-GraphJson {
    param(
        [Parameter(Mandatory)] [string] $Method,
        [Parameter(Mandatory)] [string] $Url,
        [object] $Body,
        [switch] $ForRead,
        [Parameter(Mandatory)] [string] $What
    )

    $arguments = @{
        Method             = $Method
        Uri                = $Url
        Headers            = @{ Authorization = "Bearer $(Get-GraphToken -ForRead:$ForRead)" }
        SkipHttpErrorCheck = $true
    }
    if ($null -ne $Body) {
        $arguments.Body = ($Body | ConvertTo-Json -Depth 8)
        $arguments.ContentType = 'application/json'
    }

    $response = Invoke-WebRequest @arguments

    if ($response.StatusCode -eq 403 -and $ForRead) {
        throw "Reading channel messages was refused (403). The Azure CLI needs the $ReadScope delegated permission for this user: run ./scripts/Start-TeamsOperatorRelay.ps1 -GrantReadConsent as an admin, then retry."
    }
    if ($response.StatusCode -ge 400) {
        throw "$What failed with HTTP $($response.StatusCode): $([Text.Encoding]::UTF8.GetString($response.Content))"
    }
    $text = [Text.Encoding]::UTF8.GetString($response.Content)
    if ([string]::IsNullOrWhiteSpace($text)) { return $null }
    return $text | ConvertFrom-Json
}

Write-Step 'Checking prerequisites'

if (-not (Get-Command az -ErrorAction SilentlyContinue)) { throw 'az is not on PATH.' }

$me = Invoke-GraphJson -Method get -Url "$GraphBase/me?`$select=id,userPrincipalName,displayName" -What 'Reading the signed-in user'
Write-Note "Relaying as: $($me.userPrincipalName)"

# ---------------------------------------------------------------------------------------------
# Optional one-time setup: the read consent, principal-scoped to this user alone.
# ---------------------------------------------------------------------------------------------

if ($GrantReadConsent) {
    Write-Step "Granting $ReadScope to the Azure CLI for $($me.userPrincipalName) only"

    $azCliSp = (Invoke-GraphJson -Method get -Url "$GraphBase/servicePrincipals?`$filter=appId eq '$AzureCliAppId'&`$select=id" -What 'Finding the Azure CLI service principal').value[0].id
    $graphSp = (Invoke-GraphJson -Method get -Url "$GraphBase/servicePrincipals?`$filter=appId eq '$GraphResourceAppId'&`$select=id" -What 'Finding the Graph service principal').value[0].id

    $grants = @((Invoke-GraphJson -Method get -Url "$GraphBase/oauth2PermissionGrants?`$filter=clientId eq '$azCliSp' and resourceId eq '$graphSp' and principalId eq '$($me.id)'" -What 'Reading existing grants').value)

    if ($grants.Count -gt 0) {
        # Merged, never replaced - rewriting the grant to just this scope would revoke whatever
        # the user's CLI already carried. Exact space-separated tokens, not substrings.
        $grant = $grants[0]
        $scopes = @("$($grant.scope)".Split(' ', [StringSplitOptions]::RemoveEmptyEntries))

        if ($scopes -ccontains $ReadScope) {
            Write-Exists "$ReadScope is already granted"
        }
        elseif ($PSCmdlet.ShouldProcess($me.userPrincipalName, "Extend the Azure CLI grant with $ReadScope")) {
            Invoke-GraphJson -Method patch -Url "$GraphBase/oauth2PermissionGrants/$($grant.id)" -What 'Extending the grant' -Body @{
                scope = (@($scopes + $ReadScope) -join ' ')
            } | Out-Null
            Write-Created "$ReadScope added (existing scopes preserved)"
        }
    }
    elseif ($PSCmdlet.ShouldProcess($me.userPrincipalName, "Create a principal-scoped Azure CLI grant with $ReadScope")) {
        Invoke-GraphJson -Method post -Url "$GraphBase/oauth2PermissionGrants" -What 'Creating the grant' -Body @{
            clientId    = $azCliSp
            consentType = 'Principal'
            principalId = $me.id
            resourceId  = $graphSp
            scope       = $ReadScope
        } | Out-Null
        Write-Created "$ReadScope granted, principal-scoped - this user only, never the tenant"
    }

    Write-Note 'Done. Run the relay without -GrantReadConsent to start it.'
    return
}

# ---------------------------------------------------------------------------------------------
# Resolve the stage: the team, the channel, and the agent behind them.
# ---------------------------------------------------------------------------------------------

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

Write-Step "Resolving '$TeamName' / '$ChannelName'"

$teams = @((Invoke-GraphJson -Method get -Url "$GraphBase/me/joinedTeams" -What 'Listing joined teams').value | Where-Object { $_.displayName -eq $TeamName })
if ($teams.Count -ne 1) {
    throw "Expected exactly one joined team named '$TeamName'; found $($teams.Count)."
}
$teamId = $teams[0].id

$channels = @((Invoke-GraphJson -Method get -Url "$GraphBase/teams/$teamId/channels" -What 'Listing channels').value | Where-Object { $_.displayName -eq $ChannelName })
if ($channels.Count -ne 1) {
    throw "Expected exactly one channel named '$ChannelName' in '$TeamName'; found $($channels.Count)."
}
$channelId = $channels[0].id

Write-Note "Team:    $teamId"
Write-Note "Channel: $channelId"
Write-Note "Agent:   $ProjectEndpoint -> $AgentName"

function Invoke-HostedAgent {
    param([Parameter(Mandatory)] [string] $Question)

    $token = (Invoke-Checked { az account get-access-token --resource https://ai.azure.com --query accessToken --output tsv } 'Getting a Foundry token').Trim()
    $body = @{ input = $Question; store = $false } | ConvertTo-Json

    $response = Invoke-RestMethod -Method Post `
        -Uri "$ProjectEndpoint/agents/$AgentName/endpoint/protocols/openai/responses?api-version=v1" `
        -Headers @{ Authorization = "Bearer $token" } -ContentType 'application/json' -Body $body -TimeoutSec 180

    $consent = @($response.output | Where-Object { "$($_.type)" -eq 'oauth_consent_request' }) | Select-Object -First 1
    if ($consent) {
        return [pscustomobject]@{
            Text       = "Work IQ needs the relay runner's consent before it can search their Microsoft 365. $($me.displayName): open the consent link printed in the relay terminal, then ask again."
            ConsentUrl = $consent.consent_link
            ResponseId = $response.id
        }
    }

    $text = (@($response.output | ForEach-Object { $_.content } | Where-Object { $_ -and $_.text } | ForEach-Object { $_.text }) -join "`n`n").Trim()
    if (-not $text) { $text = "The hosted run ended with status '$($response.status)' and no text." }

    return [pscustomobject]@{ Text = $text; ConsentUrl = $null; ResponseId = $response.id }
}

# ---------------------------------------------------------------------------------------------
# The relay loop. New top-level messages become questions; replies carry the answer and an honest
# label. The announcement below is a root message too, so it is marked handled at birth.
# ---------------------------------------------------------------------------------------------

Write-Step 'Relay running'
Write-Warn "Identity boundary: every question is asked AS $($me.userPrincipalName) - including questions typed by others."
Write-Warn 'Work IQ evidence comes from the relay runner''s Microsoft 365, whoever asked. Ctrl+C stops the relay.'

$cutoff = (Get-Date).ToUniversalTime().AddMinutes(-$LookbackMinutes)
$handled = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)

$announcement = Invoke-GraphJson -Method post -Url "$GraphBase/teams/$teamId/channels/$channelId/messages" -What 'Announcing the relay' -Body @{
    body = @{
        contentType = 'text'
        content     = "The Caesarea Operations Agent (Foundry-hosted) is listening on this channel via a presenter-run relay. Post a message to ask it something - try: What do our maintenance records say about streetlight L-417? Note: answers are produced as $($me.displayName), whoever asks."
    }
}
[void]$handled.Add("$($announcement.id)")
Write-Beat 'Announced in the channel.'

while ($true) {
    $messages = @((Invoke-GraphJson -Method get -Url "$GraphBase/teams/$teamId/channels/$channelId/messages?`$top=20" -ForRead -What 'Reading channel messages').value)

    foreach ($message in ($messages | Sort-Object createdDateTime)) {
        if ("$($message.messageType)" -ne 'message') { continue }
        if ($handled.Contains("$($message.id)")) { continue }
        if ([DateTimeOffset]::Parse($message.createdDateTime).UtcDateTime -lt $cutoff) { continue }

        # Everything a human typed at the top level is a question; replies are conversation the
        # relay stays out of, and its own answers are replies for exactly that reason.
        $raw = "$($message.body.content)" -replace '<[^>]+>', ' '
        $question = [System.Net.WebUtility]::HtmlDecode($raw).Trim()
        [void]$handled.Add("$($message.id)")

        if (-not $question) { continue }

        $author = if ($message.from -and $message.from.user) { $message.from.user.displayName } else { 'unknown' }
        Write-Beat "Q from $author`: $($question.Substring(0, [Math]::Min(80, $question.Length)))"

        try {
            $answer = Invoke-HostedAgent -Question $question
        }
        catch {
            $answer = [pscustomobject]@{ Text = "The hosted agent could not be reached: $($_.Exception.Message)"; ConsentUrl = $null; ResponseId = $null }
        }

        if ($answer.ConsentUrl) {
            Write-Warn "Work IQ consent required - open this as $($me.userPrincipalName): $($answer.ConsentUrl)"
        }

        $footer = "`n`n— Caesarea Operations Agent (hosted on Microsoft Foundry) · relayed as $($me.displayName)"
        if ($answer.ResponseId) { $footer += " · response $("$($answer.ResponseId)".Substring(0, [Math]::Min(18, "$($answer.ResponseId)".Length)))" }

        Invoke-GraphJson -Method post -Url "$GraphBase/teams/$teamId/channels/$channelId/messages/$($message.id)/replies" -What 'Posting the answer' -Body @{
            body = @{ contentType = 'text'; content = ($answer.Text + $footer) }
        } | Out-Null

        Write-Beat "A posted ($([Math]::Min(80, $answer.Text.Length)) chars shown): $($answer.Text.Substring(0, [Math]::Min(80, $answer.Text.Length)))"
    }

    if ($Once) {
        Write-Note 'Single pass done (-Once).'
        break
    }

    Start-Sleep -Seconds $PollSeconds
}
