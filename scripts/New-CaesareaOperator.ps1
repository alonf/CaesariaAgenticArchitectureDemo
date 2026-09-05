#Requires -Version 7.0

<#
.SYNOPSIS
    Creates the "Caesarea Operator" identity the Teams relay posts as, licensed from the Agent 365
    seats the tenant already owns.

.DESCRIPTION
    The relay posts as whoever signed in to Microsoft Graph, and a demo where the presenter
    appears to answer themselves buries the story. This script mints the separate identity:

      1. An Entra user named Caesarea Operator, in the signed-in admin's domain, with a generated
         one-time password (shown once; must be changed at first sign-in).
      2. An Agent 365 licence from the existing pool - the Tier 3 seat carries TEAMS1, a mailbox,
         OneDrive and the agent-governance service plans, which is exactly the workload an agent
         teammate needs. Delegated to ./scripts/Assign-Agent365License.ps1, which is idempotent
         and refuses seats the tenant does not have.
      3. Membership in the demo team, so the operator can read and post in the channel.
      4. Graph consent for the relay's four scopes, granted for the OPERATOR alone
         (consentType Principal) against the Microsoft Graph Command Line Tools app - so the
         operator's first Connect-MgGraph does not stall on an admin-approval screen it cannot
         answer itself.

    Honest label, worth repeating from the segment doc: this is a licensed, governed - but still
    hand-made - user identity. The sanctioned evolution is the Agent 365 "AI Teammate" flow
    (a365 setup --aiteammate), which provisions the user-shaped identity as an agent; this script
    is the demonstrable-today rung below it.

    Idempotent. An existing user, licence, membership or consent reports [exists] untouched.

.PARAMETER UserPrincipalName
    UPN for the operator. Defaults to caesarea-operator@<the signed-in admin's domain>.

.PARAMETER DisplayName
    Display name shown in Teams.

.PARAMETER TeamName
    The demo team to join.

.PARAMETER UsageLocation
    Two-letter country code for licensing. Defaults to the signed-in admin's own usageLocation.

.EXAMPLE
    ./scripts/New-CaesareaOperator.ps1 -WhatIf

.EXAMPLE
    ./scripts/New-CaesareaOperator.ps1
#>

[CmdletBinding(SupportsShouldProcess)]
param(
    [string] $UserPrincipalName,
    [string] $DisplayName = 'Caesarea Operator',
    [string] $TeamName = "Alon's Demos",
    [string] $UsageLocation
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$GraphBase = 'https://graph.microsoft.com/v1.0'
$GraphCliAppId = '14d82eec-204b-4c2f-b7e8-296a70dab67e'
$GraphResourceAppId = '00000003-0000-0000-c000-000000000000'
$RelayScopes = @('Team.ReadBasic.All', 'Channel.ReadBasic.All', 'ChannelMessage.Read.All', 'ChannelMessage.Send')

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

$script:GraphToken = $null

function Invoke-GraphJson {
    <#  Native Invoke-WebRequest with an az-minted token: no az.cmd quoting traps, and a 404 is an
        answer only where the caller says so. #>
    param(
        [Parameter(Mandatory)] [string] $Method,
        [Parameter(Mandatory)] [string] $Url,
        [object] $Body,
        [switch] $AllowNotFound,
        [Parameter(Mandatory)] [string] $What
    )

    if (-not $script:GraphToken) {
        $script:GraphToken = (Invoke-Checked {
            az account get-access-token --resource https://graph.microsoft.com --query accessToken --output tsv
        } 'Getting a Graph token').Trim()
    }

    $arguments = @{
        Method             = $Method
        Uri                = $Url
        Headers            = @{ Authorization = "Bearer $script:GraphToken" }
        SkipHttpErrorCheck = $true
    }
    if ($null -ne $Body) {
        $arguments.Body = ($Body | ConvertTo-Json -Depth 8)
        $arguments.ContentType = 'application/json'
    }

    $response = Invoke-WebRequest @arguments
    $text = if ($response.Content -is [byte[]]) { [Text.Encoding]::UTF8.GetString($response.Content) } else { "$($response.Content)" }

    if ($response.StatusCode -eq 404 -and $AllowNotFound) { return $null }
    if ($response.StatusCode -ge 400) {
        throw "$What failed with HTTP $($response.StatusCode): $text"
    }
    if ([string]::IsNullOrWhiteSpace($text)) { return $null }
    return $text | ConvertFrom-Json
}

Write-Step 'Checking prerequisites'

if (-not (Get-Command az -ErrorAction SilentlyContinue)) { throw 'az is not on PATH.' }

$admin = Invoke-GraphJson -Method get -Url "$GraphBase/me?`$select=id,userPrincipalName,usageLocation" -What 'Reading the signed-in admin'

if (-not $UserPrincipalName) {
    $domain = ("$($admin.userPrincipalName)" -split '@', 2)[1]
    $UserPrincipalName = "caesarea-operator@$domain"
}
if (-not $UsageLocation) {
    $UsageLocation = "$($admin.usageLocation)"
    if (-not $UsageLocation) {
        throw 'The signed-in admin has no usageLocation to inherit; pass -UsageLocation <two-letter country code>.'
    }
}

Write-Note "Operator: $UserPrincipalName ($DisplayName)"
Write-Note "Team:     $TeamName"
Write-Note "Location: $UsageLocation"

# ---------------------------------------------------------------------------------------------
# 1. The user.
# ---------------------------------------------------------------------------------------------

Write-Step "User '$UserPrincipalName'"

$operator = Invoke-GraphJson -Method get -Url "$GraphBase/users/$UserPrincipalName`?`$select=id,userPrincipalName,accountEnabled" -AllowNotFound -What 'Looking for the operator'

if ($operator) {
    Write-Exists "user ($($operator.id))"
}
elseif ($PSCmdlet.ShouldProcess($UserPrincipalName, 'Create the operator user')) {
    # A one-time password, generated rather than chosen, shown exactly once below and dead after
    # the first sign-in. Base64 of 18 random bytes covers the complexity classes.
    $bytes = [byte[]]::new(18)
    [System.Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
    $password = '!' + [Convert]::ToBase64String($bytes)

    $operator = Invoke-GraphJson -Method post -Url "$GraphBase/users" -What 'Creating the operator' -Body @{
        accountEnabled    = $true
        displayName       = $DisplayName
        mailNickname      = ($UserPrincipalName -split '@', 2)[0]
        userPrincipalName = $UserPrincipalName
        usageLocation     = $UsageLocation
        passwordProfile   = @{
            password                      = $password
            forceChangePasswordNextSignIn = $true
        }
    }

    Write-Created "user ($($operator.id))"
    Write-Host ''
    Write-Host "  One-time password (change forced at first sign-in): $password" -ForegroundColor Yellow
    Write-Host '  Shown once and not stored anywhere. Use it when the relay asks you to sign in as the operator.' -ForegroundColor Yellow
    Write-Host ''
}
else {
    Write-Note 'Skipped; nothing below can proceed without the user.'
    return
}

# ---------------------------------------------------------------------------------------------
# 2. The licence, from the pool the tenant already owns.
# ---------------------------------------------------------------------------------------------

Write-Step 'Agent 365 licence'

& "$PSScriptRoot/Assign-Agent365License.ps1" -UserPrincipalName $UserPrincipalName -UsageLocation $UsageLocation -WhatIf:$WhatIfPreference
if ($LASTEXITCODE -ne 0) {
    throw 'Licence assignment failed; see above.'
}

# ---------------------------------------------------------------------------------------------
# 3. Team membership.
# ---------------------------------------------------------------------------------------------

Write-Step "Membership in '$TeamName'"

$teams = @((Invoke-GraphJson -Method get -Url "$GraphBase/me/joinedTeams" -What 'Listing joined teams').value | Where-Object { $_.displayName -eq $TeamName })
if ($teams.Count -ne 1) {
    throw "Expected exactly one joined team named '$TeamName'; found $($teams.Count)."
}
$teamId = $teams[0].id

# Through the GROUP that backs the team, not the Teams member API: the az-minted token carries
# directory permissions (it just created a user) but not TeamMember.*, and group membership is
# team membership - Teams picks it up on its own within a few minutes.
$members = @((Invoke-GraphJson -Method get -Url "$GraphBase/groups/$teamId/members?`$select=id" -What 'Listing team members').value)

if (@($members | Where-Object { "$($_.id)" -eq "$($operator.id)" }).Count -gt 0) {
    Write-Exists 'operator is already a member'
}
elseif ($PSCmdlet.ShouldProcess($TeamName, "Add $DisplayName as a member")) {
    Invoke-GraphJson -Method post -Url "$GraphBase/groups/$teamId/members/`$ref" -What 'Adding the operator to the team' -Body @{
        '@odata.id' = "$GraphBase/directoryObjects/$($operator.id)"
    } | Out-Null
    Write-Created 'member (via the backing group; Teams reflects it within a few minutes)'
}

# ---------------------------------------------------------------------------------------------
# 4. Graph consent for the relay's scopes - for the operator alone, so its first Connect-MgGraph
#    never stalls on an admin-approval screen it cannot answer.
# ---------------------------------------------------------------------------------------------

Write-Step 'Relay consent for the operator'

$graphCliMatches = @((Invoke-GraphJson -Method get -Url "$GraphBase/servicePrincipals?`$filter=appId eq '$GraphCliAppId'&`$select=id" -What 'Finding the Graph Command Line Tools principal').value)
if ($graphCliMatches.Count -eq 0) {
    throw 'The Microsoft Graph Command Line Tools service principal is not in this tenant. Run Connect-MgGraph once as any user to materialize it, then re-run.'
}
$graphCliSp = $graphCliMatches[0].id

$graphSp = @((Invoke-GraphJson -Method get -Url "$GraphBase/servicePrincipals?`$filter=appId eq '$GraphResourceAppId'&`$select=id" -What 'Finding the Graph principal').value)[0].id

$grants = @((Invoke-GraphJson -Method get -Url "$GraphBase/oauth2PermissionGrants?`$filter=clientId eq '$graphCliSp' and resourceId eq '$graphSp' and principalId eq '$($operator.id)'" -What 'Reading the operator''s grants').value)

if ($grants.Count -gt 0) {
    # Merged, never replaced: exact space-separated scope tokens, existing ones preserved.
    $grant = $grants[0]
    $scopes = @("$($grant.scope)".Split(' ', [StringSplitOptions]::RemoveEmptyEntries))
    $missing = @($RelayScopes | Where-Object { $scopes -cnotcontains $_ })

    if ($missing.Count -eq 0) {
        Write-Exists 'all relay scopes are consented'
    }
    elseif ($PSCmdlet.ShouldProcess($UserPrincipalName, "Extend the grant with $($missing -join ', ')")) {
        Invoke-GraphJson -Method patch -Url "$GraphBase/oauth2PermissionGrants/$($grant.id)" -What 'Extending the grant' -Body @{
            scope = (@($scopes + $missing) -join ' ')
        } | Out-Null
        Write-Created "consent extended with $($missing -join ', ')"
    }
}
elseif ($PSCmdlet.ShouldProcess($UserPrincipalName, 'Create the relay consent grant')) {
    Invoke-GraphJson -Method post -Url "$GraphBase/oauth2PermissionGrants" -What 'Creating the consent grant' -Body @{
        clientId    = $graphCliSp
        consentType = 'Principal'
        principalId = $operator.id
        resourceId  = $graphSp
        scope       = ($RelayScopes -join ' ')
    } | Out-Null
    Write-Created "consent for $($RelayScopes -join ', ') - this user only"
}

Write-Step 'Done'
Write-Host @"
  The Caesarea Operator exists, is licensed from the Agent 365 pool, sits in the team, and may run
  the relay. To make the channel speak with its face:

    1. Disconnect-MgGraph            # drop the presenter's cached Graph session
    2. ./scripts/Start-TeamsOperatorRelay.ps1
       - sign in as $UserPrincipalName when the browser asks (one-time password above, change it)
       - the hosted-agent call still runs as YOUR az login - the identity split is the demo

  Licence propagation can take a few minutes; if the first post is refused, wait rather than
  changing anything.
"@ -ForegroundColor Green
