#Requires -Version 7.0

<#
.SYNOPSIS
    Checks the demo's per-machine setup, repairs what it can, and brings the whole system up.

.DESCRIPTION
    The one command to run on a new machine, or on a machine you have not presented from in a
    while. It exists because the setup it performs is exactly the kind that gets forgotten: done
    once, invisible afterwards, and discovered missing in front of an audience.

    What it checks, in order:

      1. The Command Center knows the hosted agent's project endpoint
         (CommandCenterWeb:HostedAgent:ProjectEndpoint, in user secrets - deliberately not in a
         committed file, because the endpoint names a specific tenant). Missing, it is resolved
         from the GitHub environment's FOUNDRY_PROJECT_ENDPOINT variable, or failing that from the
         Foundry account in the environment's resource group, and stored.
      2. An Azure sign-in exists for the hosted call to mint the presenter's token with.
      3. The work order in the presenter's OneDrive is current: it carries the source-of-record
         line and its expected-clearance date has not passed. The document's dates are stamped
         relative to the day it is written, so a copy from last week reads as an expired ticket -
         and a stale copy is refreshed by re-running New-CaesareaWorkOrder.ps1 -Force, using the
         Microsoft Graph session already cached on this machine. With no cached session the check
         degrades to a warning (pass -RefreshWorkOrder to sign in here and now).

    No check is fatal. The local demo needs no cloud at all, so anything unresolved is reported as
    a warning naming its fix, and the system starts anyway - the Hosting stage is then the only
    beat that will not work.

    Then it starts the Aspire AppHost in the foreground; Ctrl+C stops the whole system.

.PARAMETER Environment
    Environment whose hosted agent to point at.

.PARAMETER ProjectEndpoint
    Foundry project endpoint, when you would rather state it than have it resolved.

.PARAMETER Repository
    owner/name of the GitHub repository, used only to read FOUNDRY_PROJECT_ENDPOINT.

.PARAMETER SetupOnly
    Perform the checks and repairs but do not start the system.

.PARAMETER NoBrowser
    Do not open the Aspire dashboard in the browser when the system comes up.

.PARAMETER RefreshWorkOrder
    Sign in to Microsoft Graph interactively when no cached session exists, so the work-order
    check can run on a machine that has never run New-CaesareaWorkOrder.ps1.

.EXAMPLE
    ./scripts/Start-CaesareaDemo.ps1

.EXAMPLE
    ./scripts/Start-CaesareaDemo.ps1 -SetupOnly -WhatIf
#>

[CmdletBinding(SupportsShouldProcess)]
param(
    [ValidateSet('dev', 'prod')]
    [string] $Environment = 'dev',
    [string] $ProjectEndpoint,
    [string] $Repository,
    [switch] $SetupOnly,
    [switch] $NoBrowser,
    [switch] $RefreshWorkOrder
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$RepositoryRoot = Resolve-Path "$PSScriptRoot/.."
$WebProject = Join-Path $RepositoryRoot 'Apps/CommandCenter.Web'
$SecretName = 'CommandCenterWeb:HostedAgent:ProjectEndpoint'

function Write-Step { param([string] $Message) Write-Host "`n=== $Message" -ForegroundColor Cyan }
function Write-Exists { param([string] $Message) Write-Host "  [exists] $Message" -ForegroundColor DarkGray }
function Write-Created { param([string] $Message) Write-Host "  [set] $Message" -ForegroundColor Green }
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

Write-Step 'Checking prerequisites'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'dotnet is not on PATH.' }
Write-Note "Repository: $RepositoryRoot"

# ---------------------------------------------------------------------------------------------
# 1. The hosted agent endpoint. The same validation the app applies at startup is applied here,
#    so a bad value is refused where it can still be fixed rather than at launch.
# ---------------------------------------------------------------------------------------------

Write-Step 'Hosted agent endpoint (user secrets)'

$secretsOutput = & dotnet user-secrets list --project $WebProject 2>&1
$currentEndpoint = $null
if ($LASTEXITCODE -eq 0) {
    $match = @($secretsOutput) | Where-Object { "$_" -like "$SecretName = *" } | Select-Object -First 1
    if ($match) { $currentEndpoint = ("$match" -split ' = ', 2)[1].Trim() }
}
$global:LASTEXITCODE = 0

if ($currentEndpoint -and (-not $ProjectEndpoint -or $ProjectEndpoint -eq $currentEndpoint)) {
    Write-Exists "$SecretName = $currentEndpoint"
}
else {
    if (-not $ProjectEndpoint) {
        # First choice: the value the pipelines themselves use, read from the GitHub environment.
        if (Get-Command gh -ErrorAction SilentlyContinue) {
            if (-not $Repository) {
                $originUrl = git -C $RepositoryRoot remote get-url origin 2>&1
                if ($LASTEXITCODE -eq 0) {
                    $Repository = ("$originUrl" -replace '^.*github\.com[:/]', '' -replace '\.git$', '').Trim()
                }
                $global:LASTEXITCODE = 0
            }

            if ($Repository) {
                $value = gh api "repos/$Repository/environments/$Environment/variables/FOUNDRY_PROJECT_ENDPOINT" --jq '.value' 2>&1
                if ($LASTEXITCODE -eq 0) { $ProjectEndpoint = "$value".Trim() }
                $global:LASTEXITCODE = 0
            }
        }

        # Second choice: derive it from the Foundry account, the way Connect-WorkIQ does.
        if (-not $ProjectEndpoint -and (Get-Command az -ErrorAction SilentlyContinue)) {
            $accounts = @(az cognitiveservices account list --resource-group "rg-caesarea-$Environment" --query "[?kind=='AIServices'].name" --output tsv 2>&1)
            if ($LASTEXITCODE -eq 0 -and $accounts.Count -eq 1 -and $accounts[0]) {
                $ProjectEndpoint = "https://$("$($accounts[0])".Trim()).services.ai.azure.com/api/projects/caesarea-$Environment"
            }
            $global:LASTEXITCODE = 0
        }
    }

    if (-not $ProjectEndpoint) {
        Write-Warn 'The hosted agent endpoint could not be resolved (no reachable GitHub environment or Azure'
        Write-Warn 'resource group). The local demo is unaffected; the Hosting stage''s FOUNDRY HOSTED beat will'
        Write-Warn 'explain that it is unconfigured. To fix: sign in (gh auth login / az login) and re-run, or'
        Write-Warn "pass -ProjectEndpoint explicitly."
    }
    else {
        # The same shape rule CommandCenterWebOptions enforces at startup: a mistake here would
        # otherwise stop the app from booting at all.
        $parsed = $null
        $shapeOk = [Uri]::TryCreate($ProjectEndpoint, [UriKind]::Absolute, [ref]$parsed) -and
            $parsed.Scheme -eq 'https' -and
            $parsed.Host.EndsWith('.services.ai.azure.com', [StringComparison]::OrdinalIgnoreCase) -and
            $parsed.AbsolutePath -match '^/api/projects/[^/]+/?$' -and
            -not $parsed.UserInfo -and -not $parsed.Query -and -not $parsed.Fragment

        if (-not $shapeOk) {
            throw "Resolved endpoint '$ProjectEndpoint' is not a https://<account>.services.ai.azure.com/api/projects/<project> URL. Refusing to store it - the app would refuse it at startup anyway."
        }

        if ($PSCmdlet.ShouldProcess($SecretName, "Store '$ProjectEndpoint' in CommandCenter.Web user secrets")) {
            Invoke-Checked { dotnet user-secrets set $SecretName $ProjectEndpoint --project $WebProject } 'Storing the endpoint' | Out-Null
            Write-Created "$SecretName = $ProjectEndpoint"
        }
    }
}

# ---------------------------------------------------------------------------------------------
# 2. The sign-in the hosted call runs as. Whoever is signed in here is who the hosted agent sees,
#    and whose OneDrive Work IQ reads - that is the demo's point, so it is worth naming.
# ---------------------------------------------------------------------------------------------

Write-Step 'Azure sign-in (the identity the hosted agent will see)'

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    Write-Warn 'az is not on PATH. The local demo works; hosted asks will fail until Azure CLI is installed and signed in.'
}
else {
    $account = az account show --output json 2>&1
    if ($LASTEXITCODE -eq 0) {
        $user = ($account | ConvertFrom-Json).user.name
        Write-Exists "signed in as $user"
    }
    else {
        $global:LASTEXITCODE = 0
        Write-Warn 'Not signed in to Azure. The local demo works; hosted asks will fail. Run: az login'
    }
}

# ---------------------------------------------------------------------------------------------
# 3. The work order the hosted agent finds through Work IQ. Its dates are stamped relative to the
#    day it was written (raised three days back, clearance one day ahead), so a copy from last
#    week reads as an expired ticket on stage. Checked here because this is exactly the per-machine,
#    per-person state that is done once and forgotten.
# ---------------------------------------------------------------------------------------------

Write-Step 'The work order in your OneDrive (the Hosting stage''s evidence)'

# Folder and file match New-CaesareaWorkOrder.ps1's defaults - the one place they are defined.
$workOrderPath = 'Caesarea Smart City/WO-8732 Streetlight L-417 maintenance.md'

if (-not (Get-Module -ListAvailable Microsoft.Graph.Authentication)) {
    Write-Warn 'Microsoft.Graph.Authentication is not installed, so the work order cannot be checked.'
    Write-Warn 'The local demo is unaffected. For the hosted records beat: Install-Module Microsoft.Graph.Authentication, then ./scripts/New-CaesareaWorkOrder.ps1.'
}
else {
    Import-Module Microsoft.Graph.Authentication -ErrorAction Stop

    if (-not (Get-MgContext) -and $RefreshWorkOrder) {
        Connect-MgGraph -Scopes 'Files.ReadWrite' -NoWelcome -ErrorAction Stop
    }

    if (-not (Get-MgContext)) {
        # Deliberately no sign-in attempt by default: this script must be able to run unattended,
        # and Connect-MgGraph goes interactive whenever the machine's token cache cannot answer -
        # a login prompt nobody is watching is a hang, not a check.
        Write-Warn 'No Microsoft Graph session, so the work order cannot be checked.'
        Write-Warn 'Re-run with -RefreshWorkOrder (instant when this machine has signed in before; a browser prompt the first time),'
        Write-Warn 'or run ./scripts/New-CaesareaWorkOrder.ps1 yourself. The local demo is unaffected either way.'
    }
    else {
        $encodedPath = ($workOrderPath -split '/' | ForEach-Object { [uri]::EscapeDataString($_) }) -join '/'
        $content = $null

        try {
            $file = New-TemporaryFile
            try {
                Invoke-MgGraphRequest -Method GET -Uri "https://graph.microsoft.com/v1.0/me/drive/root:/$encodedPath`:/content" -OutputFilePath $file
                $content = Get-Content $file -Raw
            }
            finally {
                Remove-Item $file -ErrorAction SilentlyContinue
            }
        }
        catch {
            if ($_.ErrorDetails -and $_.ErrorDetails.Message -match '"code"\s*:\s*"itemNotFound"') {
                $content = $null
            }
            else {
                Write-Warn "The work order could not be read: $($_.Exception.Message)"
                Write-Warn 'Check it manually with ./scripts/New-CaesareaWorkOrder.ps1 -WhatIf.'
                $content = ''
            }
        }

        if ($null -eq $content -or ($content -ne '' -and (
            $content -notmatch 'Source of record' -or
            -not ($content -match '\*\*Expected clearance:\*\*\s*(\d{4}-\d{2}-\d{2})') -or
            [DateTime]::ParseExact($Matches[1], 'yyyy-MM-dd', $null) -lt (Get-Date).Date))) {

            $reason = if ($null -eq $content) { 'missing' } else { 'stale (old version or past its clearance date)' }

            if ($PSCmdlet.ShouldProcess($workOrderPath, "Refresh the $reason work order in OneDrive")) {
                & "$PSScriptRoot/New-CaesareaWorkOrder.ps1" -Force
                Write-Note 'Refreshed. Microsoft 365 needs a few minutes to re-index before Work IQ serves the new copy.'
            }
        }
        elseif ($content -ne '') {
            Write-Exists "work order is current (clearance $($Matches[1]))"
        }
    }
}

# ---------------------------------------------------------------------------------------------
# 4. Up.
# ---------------------------------------------------------------------------------------------

if ($SetupOnly) {
    Write-Step 'Done (setup only)'
    Write-Note 'Start the system with: dotnet run --project Caesarea.AppHost'
    return
}

Write-Step 'Starting the system'
Write-Note 'Aspire AppHost, foreground. Ctrl+C stops everything.'
Write-Note 'First hosted ask per person shows a Work IQ consent link in the Command Center - open it, consent, ask again.'

if ($PSCmdlet.ShouldProcess('Caesarea.AppHost', 'Run the Aspire AppHost')) {
    # The dashboard URL carries a one-time login token that only ever appears in this console
    # output, so the output is watched for it and the browser opened at exactly that address.
    # `dotnet run` prints the line but does not open anything (launchBrowser in launchSettings is
    # an IDE behaviour); without this, the presenter is left copy-pasting a token URL out of
    # scrollback. Two wordings are matched because the Aspire CLI changed its banner: the older
    # "Login to the dashboard at <url>" and the current "Dashboard:  <url>" - and colour codes are
    # stripped first, since an ANSI sequence glued to the URL would ride into the browser with it.
    $script:DashboardOpened = $false

    dotnet run --project (Join-Path $RepositoryRoot 'Caesarea.AppHost') 2>&1 | ForEach-Object {
        $line = "$_"
        Write-Host $line

        if (-not $NoBrowser -and -not $script:DashboardOpened) {
            $plain = $line -replace "`e\[[0-9;]*[A-Za-z]", ''
            if ($plain -match '(?:Login to the dashboard at|Dashboard:)\s+(https\S+)') {
                $script:DashboardOpened = $true
                Write-Note "Opening the Aspire dashboard: $($Matches[1])"
                Start-Process $Matches[1]
            }
        }
    }

    exit $LASTEXITCODE
}
