#Requires -Version 7.0
#Requires -Modules Microsoft.Graph.Authentication

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

    Graph access goes through Connect-MgGraph rather than the Azure CLI, and the reason is worth
    keeping: Azure CLI is a Microsoft first-party app whose Graph scopes are fixed by Microsoft
    preauthorization - a tenant admin cannot grant it ChannelMessage.Read.All, and trying earns
    AADSTS65002 ("must be configured via preauthorization"). The Graph PowerShell app accepts
    dynamic consent, so the first run prompts once (an admin must approve - the read scope is
    admin-restricted) and the machine's token cache makes every later run silent.

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

.EXAMPLE
    ./scripts/Start-TeamsOperatorRelay.ps1

.EXAMPLE
    ./scripts/Start-TeamsOperatorRelay.ps1 -Once -LookbackMinutes 5
#>

[CmdletBinding()]
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
    [switch] $Once
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$GraphBase = 'https://graph.microsoft.com/v1.0'
$RequiredScopes = @('Team.ReadBasic.All', 'Channel.ReadBasic.All', 'ChannelMessage.Read.All', 'ChannelMessage.Send')

function Write-Step { param([string] $Message) Write-Host "`n=== $Message" -ForegroundColor Cyan }
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

function Invoke-GraphJson {
    <#  Invoke-MgGraphRequest with PSObject output and one useful translation: a Forbidden on the
        message-read path names the admin-restricted scope and the fix instead of just the code. #>
    param(
        [Parameter(Mandatory)] [string] $Method,
        [Parameter(Mandatory)] [string] $Url,
        [object] $Body,
        [Parameter(Mandatory)] [string] $What
    )

    try {
        if ($null -eq $Body) {
            return Invoke-MgGraphRequest -Method $Method -Uri $Url -OutputType PSObject
        }
        return Invoke-MgGraphRequest -Method $Method -Uri $Url -Body ($Body | ConvertTo-Json -Depth 8) -ContentType 'application/json' -OutputType PSObject
    }
    catch {
        if ("$_" -match 'Forbidden|Authorization_RequestDenied' -and $What -like '*messages*') {
            throw "$What was refused. ChannelMessage.Read.All is admin-restricted: sign in as an admin once (delete the cached session with Disconnect-MgGraph, re-run, and approve the consent prompt), or have an admin pre-approve the Microsoft Graph Command Line Tools app for these scopes."
        }
        throw "$What failed: $_"
    }
}

Write-Step 'Checking prerequisites'

if (-not (Get-Command az -ErrorAction SilentlyContinue)) { throw 'az is not on PATH (the hosted agent call needs it).' }

Import-Module Microsoft.Graph.Authentication -ErrorAction Stop

# Connect-MgGraph reuses the machine's token cache silently when the scopes are already consented
# and cached; it goes interactive only when something is missing - which for the admin-restricted
# read scope is exactly once per user.
$context = Get-MgContext
$missing = if ($context) { @($RequiredScopes | Where-Object { $context.Scopes -notcontains $_ }) } else { $RequiredScopes }
if ($missing.Count -gt 0) {
    Write-Note "Connecting to Microsoft Graph for: $($RequiredScopes -join ', ')"
    Connect-MgGraph -Scopes $RequiredScopes -NoWelcome -ErrorAction Stop
}

$me = Invoke-GraphJson -Method GET -Url "$GraphBase/me?`$select=id,userPrincipalName,displayName" -What 'Reading the signed-in user'
Write-Note "Relaying as: $($me.userPrincipalName)"

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

$teams = @((Invoke-GraphJson -Method GET -Url "$GraphBase/me/joinedTeams" -What 'Listing joined teams').value | Where-Object { $_.displayName -eq $TeamName })
if ($teams.Count -ne 1) {
    throw "Expected exactly one joined team named '$TeamName'; found $($teams.Count)."
}
$teamId = $teams[0].id

$channels = @((Invoke-GraphJson -Method GET -Url "$GraphBase/teams/$teamId/channels" -What 'Listing channels').value | Where-Object { $_.displayName -eq $ChannelName })
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

$announcement = Invoke-GraphJson -Method POST -Url "$GraphBase/teams/$teamId/channels/$channelId/messages" -What 'Announcing the relay' -Body @{
    body = @{
        contentType = 'text'
        content     = "The Caesarea Operations Agent (Foundry-hosted) is listening on this channel via a presenter-run relay. Post a message to ask it something - try: What do our maintenance records say about streetlight L-417? Note: answers are produced as $($me.displayName), whoever asks."
    }
}
[void]$handled.Add("$($announcement.id)")
Write-Beat 'Announced in the channel.'

while ($true) {
    $messages = @((Invoke-GraphJson -Method GET -Url "$GraphBase/teams/$teamId/channels/$channelId/messages?`$top=20" -What 'Reading channel messages').value)

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

        Invoke-GraphJson -Method POST -Url "$GraphBase/teams/$teamId/channels/$channelId/messages/$($message.id)/replies" -What 'Posting the answer' -Body @{
            body = @{ contentType = 'text'; content = ($answer.Text + $footer) }
        } | Out-Null

        Write-Beat "A posted: $($answer.Text.Substring(0, [Math]::Min(80, $answer.Text.Length)))"
    }

    if ($Once) {
        Write-Note 'Single pass done (-Once).'
        break
    }

    Start-Sleep -Seconds $PollSeconds
}
