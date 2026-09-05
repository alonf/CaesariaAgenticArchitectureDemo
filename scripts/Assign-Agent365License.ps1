#Requires -Version 7.0

<#
.SYNOPSIS
    Assigns a Microsoft Agent 365 licence to a user, so agent governance and observability work.

.DESCRIPTION
    Agent 365 needs at least one assigned licence in the tenant before it does anything visible. With
    none assigned it does not fail: the portal loads, the agent runs, and telemetry is simply dropped.
    That reads as a broken integration rather than a missing licence, and it is the reason this script
    exists and runs early rather than at the end.

    Defaults to the signed-in user, so a reader can run it against their own tenant without editing
    anything. Nobody's identity is written into the repository.

    Idempotent: an already-licensed user is reported and left alone.

    Two prerequisites the Graph API will not explain clearly if they are missing:

      - The caller needs a directory role that can assign licences - User Administrator, License
        Administrator or Global Administrator. A plain member gets Authorization_RequestDenied.
      - The target user needs a usageLocation. Licence assignment fails without one, and the error
        does not say so. Pass -UsageLocation to set it.

.PARAMETER UserPrincipalName
    User to license. Defaults to the signed-in account.

.PARAMETER SkuPartNumber
    The Agent 365 SKU to assign.

.PARAMETER UsageLocation
    Two-letter ISO country code to set on the user when they have none. Licence assignment requires
    it. Only written when the user's usageLocation is empty; an existing value is never changed.

.EXAMPLE
    ./scripts/Assign-Agent365License.ps1 -WhatIf

.EXAMPLE
    ./scripts/Assign-Agent365License.ps1

.EXAMPLE
    ./scripts/Assign-Agent365License.ps1 -UserPrincipalName someone@contoso.com -UsageLocation IL
#>

[CmdletBinding(SupportsShouldProcess)]
param(
    [string] $UserPrincipalName,
    [string] $SkuPartNumber = 'MICROSOFT_AGENT_365_TIER_3',
    [string] $UsageLocation
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$GraphBase = 'https://graph.microsoft.com/v1.0'

function Write-Step { param([string] $Message) Write-Host "`n=== $Message" -ForegroundColor Cyan }
function Write-Exists { param([string] $Message) Write-Host "  [exists] $Message" -ForegroundColor DarkGray }
function Write-Created { param([string] $Message) Write-Host "  [assigned] $Message" -ForegroundColor Green }
function Write-Note { param([string] $Message) Write-Host "  $Message" -ForegroundColor DarkGray }

function Invoke-Checked {
    param([Parameter(Mandatory)] [scriptblock] $Command, [Parameter(Mandatory)] [string] $What)

    $output = & $Command 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "$What failed (exit $LASTEXITCODE): $($output -join [Environment]::NewLine)"
    }
    return $output
}

function Invoke-Graph {
    <#  Graph through `az rest`, with any body written to a file. Inline JSON through az on Windows
        means fighting two layers of quoting, and it fails by silent truncation rather than error. #>
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

if (-not (Get-Command az -ErrorAction SilentlyContinue)) { throw 'az is not on PATH.' }

$account = (Invoke-Checked { az account show --output json } 'Reading the current Azure account') | ConvertFrom-Json
Write-Note "Tenant: $($account.tenantId)"

if (-not $UserPrincipalName) {
    $me = Invoke-Graph -Method get -Url "$GraphBase/me?`$select=userPrincipalName" -What 'Reading the signed-in user'
    $UserPrincipalName = $me.userPrincipalName
    Write-Note "User:   $UserPrincipalName (signed in; pass -UserPrincipalName to target another)"
}
else {
    Write-Note "User:   $UserPrincipalName"
}

# ---------------------------------------------------------------------------------------------
# The subscription.
# ---------------------------------------------------------------------------------------------

Write-Step "Licence '$SkuPartNumber'"

$skus = Invoke-Graph -Method get -Url "$GraphBase/subscribedSkus" -What 'Listing subscribed SKUs'
$sku = $skus.value | Where-Object { $_.skuPartNumber -eq $SkuPartNumber } | Select-Object -First 1

if (-not $sku) {
    $available = ($skus.value | ForEach-Object { $_.skuPartNumber }) -join ', '
    throw "No subscription for '$SkuPartNumber' in this tenant. Available: $available"
}

$free = $sku.prepaidUnits.enabled - $sku.consumedUnits
Write-Note "SKU id: $($sku.skuId)"
Write-Note "Units:  $($sku.consumedUnits) of $($sku.prepaidUnits.enabled) used, $free free"

# Deliberately NOT failing on zero free units here. With one purchased seat, a successful first run
# consumes it - and a free-units gate placed before the already-assigned check would then fail every
# later run of an idempotent script. Whether a free unit is needed depends on whether the target
# already holds the licence, so the check lives beside the assignment.

# ---------------------------------------------------------------------------------------------
# The user.
# ---------------------------------------------------------------------------------------------

Write-Step 'User'

$user = Invoke-Graph -Method get `
    -Url "$GraphBase/users/$UserPrincipalName`?`$select=id,userPrincipalName,usageLocation,accountEnabled" `
    -What "Reading user '$UserPrincipalName'"

Write-Note "Object id:      $($user.id)"
Write-Note "Enabled:        $($user.accountEnabled)"
Write-Note "Usage location: $(if ($user.usageLocation) { $user.usageLocation } else { '(none)' })"

# ---------------------------------------------------------------------------------------------
# The assignment. Held-licence check first: everything else - the free unit, the usage location,
# even a disabled account - only matters when a NEW assignment is about to happen.
# ---------------------------------------------------------------------------------------------

Write-Step 'Assignment'

$licenses = Invoke-Graph -Method get -Url "$GraphBase/users/$($user.id)/licenseDetails" -What 'Reading current licences'
$held = @($licenses.value | Where-Object { $_.skuId -eq $sku.skuId })

if ($held.Count -gt 0) {
    Write-Exists "$SkuPartNumber is already assigned to $UserPrincipalName"
}
else {
    # A licence on a disabled account satisfies Agent 365's "at least one licensed user" on paper
    # while governing nothing anyone can use - and it burns the seat.
    if (-not $user.accountEnabled) {
        throw "User '$UserPrincipalName' is disabled. Assigning a licence to a disabled account spends a seat on nothing; enable the account or pick another user."
    }

    if ($free -le 0) {
        throw "No free units of '$SkuPartNumber'. Release one, or buy more, before assigning."
    }

    if (-not $user.usageLocation) {
        if (-not $UsageLocation) {
            throw "User '$UserPrincipalName' has no usageLocation, and licence assignment requires one. Re-run with -UsageLocation <two-letter country code>, e.g. -UsageLocation IL."
        }

        if ($PSCmdlet.ShouldProcess($UserPrincipalName, "Set usageLocation to $UsageLocation")) {
            Invoke-Graph -Method patch -Url "$GraphBase/users/$($user.id)" -What 'Setting usageLocation' -Body @{
                usageLocation = $UsageLocation
            } | Out-Null
            Write-Created "usageLocation = $UsageLocation"
        }
    }
    elseif ($UsageLocation -and $UsageLocation -ne $user.usageLocation) {
        # Never silently changed: it has tax and compliance meaning, and this script's job is licensing.
        Write-Note "-UsageLocation $UsageLocation ignored; the user already has $($user.usageLocation)."
    }

    if ($PSCmdlet.ShouldProcess($UserPrincipalName, "Assign $SkuPartNumber")) {
        Invoke-Graph -Method post -Url "$GraphBase/users/$($user.id)/assignLicense" -What 'Assigning the licence' -Body @{
            addLicenses    = @(@{ skuId = $sku.skuId; disabledPlans = @() })
            removeLicenses = @()
        } | Out-Null

        Write-Created "$SkuPartNumber to $UserPrincipalName"
    }
}

Write-Step 'Done'
Write-Host @"
  Agent 365 has a licensed user in this tenant, which is what it needs before governance and
  observability show anything. Assignment can take a few minutes to propagate; if the portal still
  shows nothing, wait before changing anything.

  Note that the licence must be in the SAME tenant as the Foundry project the agents run in
  ($($account.tenantId)). A licence in another tenant governs nothing here.
"@ -ForegroundColor Green
