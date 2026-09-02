#Requires -Version 5.1
<#
.SYNOPSIS
    Standardizes Thunderbird message-tag definitions across every profile on this PC.

.DESCRIPTION
    Finds all Thunderbird profiles via %APPDATA%\Thunderbird\profiles.ini (honouring
    IsRelative=0/1 and the [InstallXXXX] default-profile sections) and writes the tag
    table below into each profile's user.js.

    user.js is written as UTF-8 WITHOUT a BOM, one pref per line, so Greek tag names
    are not garbled. Lines in an existing user.js that are not mailnews.tags.* prefs
    are preserved; the original is backed up first. prefs.js is backed up once but
    never modified (except by -Remove, which restores the newest backup).

    NOTE: this .ps1 must stay saved as UTF-8 WITH BOM. Windows PowerShell 5.1 reads a
    BOM-less script as ANSI and would corrupt the Greek names below. The script
    checks this at startup and refuses to run if the BOM is missing.

.PARAMETER Remove
    Strip all mailnews.tags.* lines from user.js and restore the newest prefs.js backup.

.PARAMETER ThunderbirdRoot
    Override the Thunderbird data directory (default: %APPDATA%\Thunderbird).

.EXAMPLE
    .\deploy-tb-tags.ps1 -WhatIf
.EXAMPLE
    .\deploy-tb-tags.ps1
.EXAMPLE
    .\deploy-tb-tags.ps1 -Remove -WhatIf
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [switch] $Remove,
    [string] $ThunderbirdRoot = (Join-Path $env:APPDATA 'Thunderbird')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ---------------------------------------------------------------------------
# TAG TABLE - edit this block only. Nothing below depends on its contents.
#   key     : lowercase latin / digits / underscore, must match ^[a-z0-9_]+$
#   name    : display name, may contain Greek (user.js is written UTF-8 no BOM)
#   color   : #RRGGBB
#   ordinal : string, fixed width, controls sort order (10, 20, 30, ...)
# ---------------------------------------------------------------------------
$TagTable = @(
    @{ key = 'giannis'; name = 'Γιάννης'; color = '#E53935'; ordinal = '10' }
    @{ key = 'maria';   name = 'Μαρία';   color = '#1E88E5'; ordinal = '20' }
    @{ key = 'kostas';  name = 'Κώστας';  color = '#43A047'; ordinal = '30' }
)
# ---------------------------------------------------------------------------

$NL          = "`r`n"                       # one line ending, used everywhere
$PrefPrefix  = 'mailnews.tags.'
$BeginMarker = '// --- BEGIN managed tag block (deploy-tb-tags.ps1) ---'
$EndMarker   = '// --- END managed tag block ---'
$Stamp       = Get-Date -Format 'yyyyMMdd-HHmmss'
$Utf8NoBom   = New-Object System.Text.UTF8Encoding($false)
$Summary     = New-Object System.Collections.Generic.List[object]


# ===========================================================================
# Helpers
# ===========================================================================

function Assert-ScriptEncoding {
    # PowerShell 5.1 reads a BOM-less .ps1 as ANSI, which mangles the Greek
    # literals in $TagTable before this script ever runs. Catch that early.
    if (-not $PSCommandPath -or -not (Test-Path -LiteralPath $PSCommandPath)) { return }
    $needsUnicode = @($TagTable | Where-Object { $_.name -match '[^\x00-\x7F]' }).Count -gt 0
    if (-not $needsUnicode) { return }

    $head = New-Object byte[] 3
    $read = 0
    $fs = [System.IO.File]::OpenRead($PSCommandPath)
    try { $read = $fs.Read($head, 0, 3) } finally { $fs.Dispose() }

    $utf8Bom  = ($read -ge 3 -and $head[0] -eq 0xEF -and $head[1] -eq 0xBB -and $head[2] -eq 0xBF)
    $utf16Bom = ($read -ge 2 -and (($head[0] -eq 0xFF -and $head[1] -eq 0xFE) -or
                                   ($head[0] -eq 0xFE -and $head[1] -eq 0xFF)))
    if (-not ($utf8Bom -or $utf16Bom)) {
        throw ("This script contains non-ASCII tag names but is saved without a BOM. " +
               "Windows PowerShell 5.1 would read it as ANSI and corrupt them. " +
               "Re-save '$PSCommandPath' as 'UTF-8 with BOM' and run it again.")
    }
}

function Assert-TagTable {
    $problems = New-Object System.Collections.Generic.List[string]

    if (@($TagTable).Count -eq 0) { $problems.Add('The tag table is empty.') }

    $i = 0
    foreach ($t in $TagTable) {
        $i++
        foreach ($field in 'key', 'name', 'color', 'ordinal') {
            if (-not $t.ContainsKey($field)) { $problems.Add("entry #$i is missing the '$field' field.") }
        }
        if (-not $t.ContainsKey('key') -or -not $t.ContainsKey('name') -or
            -not $t.ContainsKey('color') -or -not $t.ContainsKey('ordinal')) { continue }

        $label = "tag '$($t.key)'"
        if ($t.key     -cnotmatch '^[a-z0-9_]+$')      { $problems.Add("$label : key must match ^[a-z0-9_]+`$ (lowercase latin, digits, underscore).") }
        if ($t.color   -notmatch  '^#[0-9A-Fa-f]{6}$') { $problems.Add("$label : color '$($t.color)' must match ^#[0-9A-Fa-f]{6}`$.") }
        if ($t.ordinal -notmatch  '^[0-9]+$')          { $problems.Add("$label : ordinal '$($t.ordinal)' must be digits only.") }
        if ([string]::IsNullOrWhiteSpace($t.name))     { $problems.Add("$label : name is empty.") }
        if ($t.name -match '[\r\n]')                   { $problems.Add("$label : name contains a line break.") }
    }

    foreach ($field in 'key', 'ordinal') {
        $dupes = $TagTable | Where-Object { $_.ContainsKey($field) } |
                 Group-Object -Property { $_[$field] } | Where-Object { $_.Count -gt 1 }
        foreach ($d in $dupes) { $problems.Add("duplicate ${field}: '$($d.Name)' used $($d.Count) times.") }
    }

    if ($problems.Count -gt 0) {
        throw ("Tag table validation failed:" + $NL + '  - ' + ($problems -join ($NL + '  - ')))
    }

    # Thunderbird sorts ordinals as strings, so mixed widths sort unexpectedly.
    $widths = @($TagTable | ForEach-Object { $_.ordinal.Length } | Sort-Object -Unique)
    if ($widths.Count -gt 1) {
        Write-Warning ("Ordinals have mixed widths ($($widths -join ', ')). Thunderbird sorts " +
                       "them as strings, so '9' sorts after '10'. Pad them to a fixed width.")
    }
}

function Get-IniContent {
    param([Parameter(Mandatory)][string] $Path)
    $ini     = [ordered]@{}
    $section = $null
    foreach ($raw in [System.IO.File]::ReadAllLines($Path, [System.Text.Encoding]::UTF8)) {
        $line = $raw.Trim()
        if ($line -eq '' -or $line.StartsWith(';') -or $line.StartsWith('#')) { continue }
        if ($line -match '^\[(.+)\]$') { $section = $Matches[1]; $ini[$section] = [ordered]@{}; continue }
        if ($null -eq $section) { continue }
        $eq = $line.IndexOf('=')
        if ($eq -lt 1) { continue }
        $ini[$section][$line.Substring(0, $eq).Trim()] = $line.Substring($eq + 1).Trim()
    }
    return $ini
}

function Resolve-ProfilePath {
    param([string] $Path, [bool] $IsRelative, [string] $Root)
    $p = $Path -replace '/', '\'
    if ($IsRelative -or -not [System.IO.Path]::IsPathRooted($p)) { $p = Join-Path $Root $p }
    return [System.IO.Path]::GetFullPath($p)
}

function Get-ThunderbirdProfile {
    param([Parameter(Mandatory)][string] $Root)

    $iniPath = Join-Path $Root 'profiles.ini'
    if (-not (Test-Path -LiteralPath $iniPath)) {
        throw "profiles.ini not found at '$iniPath'. Is Thunderbird installed for this user?"
    }
    $ini = Get-IniContent -Path $iniPath

    $found = [ordered]@{}   # lowercased full path -> profile object
    $add = {
        param($FullPath, $Name, $Source, $IsDefault)
        $k = $FullPath.ToLowerInvariant()
        if ($found.Contains($k)) {
            if ($IsDefault) { $found[$k].IsDefault = $true }
            if ($found[$k].Source -notlike "*$Source*") { $found[$k].Source += ", $Source" }
            return
        }
        $found[$k] = [pscustomobject]@{
            Name      = $Name
            Path      = $FullPath
            Source    = $Source
            IsDefault = [bool]$IsDefault
            Exists    = (Test-Path -LiteralPath $FullPath -PathType Container)
        }
    }

    foreach ($section in $ini.Keys) {
        if ($section -notmatch '^Profile\d+$') { continue }
        $s = $ini[$section]
        if (-not $s.Contains('Path')) { Write-Warning "[$section] has no Path= entry; skipped."; continue }
        $isRel = $true
        if ($s.Contains('IsRelative')) { $isRel = ($s['IsRelative'] -eq '1') }
        $name  = if ($s.Contains('Name')) { $s['Name'] } else { $section }
        $isDef = ($s.Contains('Default') -and $s['Default'] -eq '1')
        & $add (Resolve-ProfilePath -Path $s['Path'] -IsRelative $isRel -Root $Root) $name $section $isDef
    }

    # [InstallXXXXXXXX] sections name the default profile of an installation.
    # Their Default= value is a path relative to the profiles.ini directory.
    foreach ($section in $ini.Keys) {
        if ($section -notmatch '^Install') { continue }
        $s = $ini[$section]
        if (-not $s.Contains('Default')) { continue }
        $full = Resolve-ProfilePath -Path $s['Default'] -IsRelative $false -Root $Root
        & $add $full (Split-Path -Leaf $full) $section $true
    }

    return @($found.Values)
}

function ConvertTo-JsString {
    param([string] $Value)
    # one backslash -> two backslashes, then " -> \"
    return ($Value -replace '\\', '\\' -replace '"', '\"')
}

function New-TagPrefLine {
    param([Parameter(Mandatory)] $Tags)
    $lines = New-Object System.Collections.Generic.List[string]
    foreach ($t in ($Tags | Sort-Object -Property @{ Expression = { [int]$_.ordinal } })) {
        $key = $t.key
        $lines.Add(('user_pref("{0}{1}.tag", "{2}");'     -f $PrefPrefix, $key, (ConvertTo-JsString $t.name)))
        $lines.Add(('user_pref("{0}{1}.color", "{2}");'   -f $PrefPrefix, $key, (ConvertTo-JsString $t.color)))
        $lines.Add(('user_pref("{0}{1}.ordinal", "{2}");' -f $PrefPrefix, $key, (ConvertTo-JsString $t.ordinal)))
    }
    return $lines
}

function Get-ForeignLine {
    # Everything in user.js that is not ours: no mailnews.tags.* prefs, no markers.
    param([string] $Content)
    $kept = @($Content -split "\r?\n" | Where-Object {
        ($_ -notmatch ('^\s*user_pref\(\s*"' + [regex]::Escape($PrefPrefix))) -and
        ($_ -ne $BeginMarker) -and ($_ -ne $EndMarker)
    })
    # Drop trailing blank lines so re-runs do not accumulate them. Walk an index
    # instead of slicing: $kept[0..($kept.Count - 2)] becomes $kept[0..-1] on a
    # one-element array, which PowerShell expands to two elements, not zero.
    $last = $kept.Count - 1
    while ($last -ge 0 -and [string]::IsNullOrWhiteSpace($kept[$last])) { $last-- }
    if ($last -lt 0) { return @() }
    return @($kept[0..$last])
}

function Measure-TagLine {
    param([string] $Content)
    return @($Content -split "\r?\n" | Where-Object {
        $_ -match ('^\s*user_pref\(\s*"' + [regex]::Escape($PrefPrefix)) }).Count
}

function Write-Utf8NoBom {
    param([Parameter(Mandatory)][string] $Path, [Parameter(Mandatory)][string] $Text)
    [System.IO.File]::WriteAllText($Path, $Text, $Utf8NoBom)
}

function Read-TextFile {
    param([Parameter(Mandatory)][string] $Path)
    return [System.IO.File]::ReadAllText($Path, [System.Text.Encoding]::UTF8)
}

function Invoke-Backup {
    param([string] $Path, [string] $BackupPath)
    if ($PSCmdlet.ShouldProcess($Path, "Back up to $(Split-Path -Leaf $BackupPath)")) {
        Copy-Item -LiteralPath $Path -Destination $BackupPath -Force
        return $true
    }
    return $false
}

function Add-Summary {
    param([string] $ProfilePath, [string] $File, [string] $Action, [string] $Detail)
    $Summary.Add([pscustomobject]@{
        Profile = $ProfilePath
        File    = $File
        Action  = $Action
        Detail  = $Detail
    })
}


# ===========================================================================
# Pre-flight
# ===========================================================================

Assert-ScriptEncoding
Assert-TagTable

$running = @(Get-Process -Name thunderbird -ErrorAction SilentlyContinue)
if ($running.Count -gt 0) {
    $pids = ($running | ForEach-Object { $_.Id }) -join ', '
    Write-Error ("Thunderbird is running (PID $pids). Close it and re-run this script - " +
                 "Thunderbird rewrites prefs.js on exit and would overwrite these changes. " +
                 "This script will not close it for you.") -ErrorAction Stop
}

if (-not (Test-Path -LiteralPath $ThunderbirdRoot -PathType Container)) {
    throw "Thunderbird directory not found: '$ThunderbirdRoot'."
}

$allProfiles = @(Get-ThunderbirdProfile -Root $ThunderbirdRoot)
$missing     = @($allProfiles | Where-Object { -not $_.Exists })
foreach ($m in $missing) {
    Write-Warning "Profile '$($m.Name)' from profiles.ini does not exist on disk: $($m.Path) - skipped."
    Add-Summary $m.Path '-' 'skipped' 'directory does not exist'
}
$targets = @($allProfiles | Where-Object { $_.Exists })
if ($targets.Count -eq 0) { throw 'No existing Thunderbird profile directories were found.' }

$mode = if ($Remove) { 'REMOVE' } else { 'DEPLOY' }
Write-Host ''
Write-Host "deploy-tb-tags.ps1 - mode: $mode$(if ($WhatIfPreference) { '   (-WhatIf: nothing will be changed)' })" -ForegroundColor Cyan
Write-Host "Thunderbird root : $ThunderbirdRoot"
Write-Host "Profiles found   : $($targets.Count) target(s)$(if ($missing.Count) { ", $($missing.Count) missing" })"
if (-not $Remove) {
    Write-Host "Tags to write    : $(@($TagTable).Count) ($((@($TagTable | ForEach-Object { $_.key })) -join ', '))"
}
Write-Host ''


# ===========================================================================
# Main
# ===========================================================================

foreach ($p in $targets) {
    $userJs  = Join-Path $p.Path 'user.js'
    $prefsJs = Join-Path $p.Path 'prefs.js'
    $flag    = if ($p.IsDefault) { ' [default]' } else { '' }
    Write-Host "Profile: $($p.Name)$flag   (from $($p.Source))" -ForegroundColor Yellow
    Write-Host "         $($p.Path)"

    $exists   = Test-Path -LiteralPath $userJs -PathType Leaf
    $existing = if ($exists) { Read-TextFile $userJs } else { $null }
    $kept     = @(if ($exists) { Get-ForeignLine -Content $existing } else { @() })

    if ($Remove) {
        # ---- user.js: strip the tag prefs --------------------------------
        if (-not $exists) {
            Write-Host '         user.js  : not present, nothing to strip'
            Add-Summary $p.Path 'user.js' 'skipped' 'not present'
        }
        else {
            $tagCount = Measure-TagLine -Content $existing
            if ($tagCount -eq 0) {
                Write-Host '         user.js  : no mailnews.tags.* lines, left untouched'
                Add-Summary $p.Path 'user.js' 'unchanged' 'no tag prefs present'
            }
            else {
                $backup   = "$userJs.bak.$Stamp"
                $didBak   = Invoke-Backup -Path $userJs -BackupPath $backup
                $bakNote  = if ($didBak) { Split-Path -Leaf $backup } else { '(whatif) ' + (Split-Path -Leaf $backup) }
                if ($kept.Count -eq 0) {
                    if ($PSCmdlet.ShouldProcess($userJs, "Delete (only $tagCount tag pref line(s) remained)")) {
                        Remove-Item -LiteralPath $userJs -Force
                    }
                    Write-Host "         user.js  : $tagCount tag pref line(s) removed, file deleted (nothing else in it)"
                    Add-Summary $p.Path 'user.js' 'deleted' "$tagCount prefs removed; backup: $bakNote"
                }
                else {
                    $newText = ($kept -join $NL) + $NL
                    if ($PSCmdlet.ShouldProcess($userJs, "Strip $tagCount tag pref line(s), keep $($kept.Count) other line(s)")) {
                        Write-Utf8NoBom -Path $userJs -Text $newText
                    }
                    Write-Host "         user.js  : $tagCount tag pref line(s) removed, $($kept.Count) other line(s) kept"
                    Add-Summary $p.Path 'user.js' 'stripped' "$tagCount removed, $($kept.Count) kept; backup: $bakNote"
                }
            }
        }

        # ---- prefs.js: restore the newest backup -------------------------
        $backups = @(Get-ChildItem -LiteralPath $p.Path -Filter 'prefs.js.bak.*' -File -ErrorAction SilentlyContinue |
                     Sort-Object LastWriteTime -Descending)
        if ($backups.Count -eq 0) {
            Write-Warning "No prefs.js.bak.* found in $($p.Path) - nothing to restore."
            Add-Summary $p.Path 'prefs.js' 'skipped' 'no backup to restore'
        }
        else {
            $newest = $backups[0]
            if (Test-Path -LiteralPath $prefsJs -PathType Leaf) {
                [void](Invoke-Backup -Path $prefsJs -BackupPath "$prefsJs.prerestore.$Stamp")
            }
            if ($PSCmdlet.ShouldProcess($prefsJs, "Restore from $($newest.Name)")) {
                Copy-Item -LiteralPath $newest.FullName -Destination $prefsJs -Force
            }
            Write-Host "         prefs.js : restored from $($newest.Name)"
            Add-Summary $p.Path 'prefs.js' 'restored' "from $($newest.Name)"
        }
    }
    else {
        # ---- prefs.js: one-time safety backup, never modified ------------
        if (Test-Path -LiteralPath $prefsJs -PathType Leaf) {
            $hasBackup = @(Get-ChildItem -LiteralPath $p.Path -Filter 'prefs.js.bak.*' -File -ErrorAction SilentlyContinue).Count -gt 0
            if ($hasBackup) {
                Write-Host '         prefs.js : backup already exists, not modified'
                Add-Summary $p.Path 'prefs.js' 'unchanged' 'backup already exists'
            }
            else {
                $pb = "$prefsJs.bak.$Stamp"
                [void](Invoke-Backup -Path $prefsJs -BackupPath $pb)
                Write-Host "         prefs.js : backed up to $(Split-Path -Leaf $pb) (not modified)"
                Add-Summary $p.Path 'prefs.js' 'backed up' (Split-Path -Leaf $pb)
            }
        }
        else {
            Write-Host '         prefs.js : not present (profile never started), nothing to back up'
            Add-Summary $p.Path 'prefs.js' 'skipped' 'not present'
        }

        # ---- user.js: rewrite the managed tag block ----------------------
        $tagLines = @(New-TagPrefLine -Tags $TagTable)
        $block    = @($BeginMarker) + $tagLines + @($EndMarker)
        $allLines = if ($kept.Count -gt 0) { $kept + @('') + $block } else { $block }
        $newText  = ($allLines -join $NL) + $NL

        if ($exists -and $existing -ceq $newText) {
            Write-Host "         user.js  : already up to date ($(@($TagTable).Count) tags), not rewritten"
            Add-Summary $p.Path 'user.js' 'unchanged' "$(@($TagTable).Count) tags already correct"
        }
        else {
            $bakNote = 'no previous file'
            if ($exists) {
                $backup  = "$userJs.bak.$Stamp"
                $didBak  = Invoke-Backup -Path $userJs -BackupPath $backup
                $bakNote = if ($didBak) { Split-Path -Leaf $backup } else { '(whatif) ' + (Split-Path -Leaf $backup) }
            }
            $verb = if ($exists) { 'Rewrite' } else { 'Create' }
            $past = if ($exists) { 'rewritten' } else { 'created' }
            $old  = if ($exists) { Measure-TagLine -Content $existing } else { 0 }
            if ($PSCmdlet.ShouldProcess($userJs, "$verb with $($tagLines.Count) tag pref line(s) (UTF-8, no BOM), preserving $($kept.Count) other line(s)")) {
                Write-Utf8NoBom -Path $userJs -Text $newText
            }
            Write-Host ("         user.js  : $past - $($tagLines.Count) pref line(s) for $(@($TagTable).Count) tag(s), " +
                        "$($kept.Count) other line(s) preserved, $old old tag line(s) replaced")
            Add-Summary $p.Path 'user.js' $past "$(@($TagTable).Count) tags / $($tagLines.Count) prefs; kept $($kept.Count); backup: $bakNote"
        }
    }
    Write-Host ''
}


# ===========================================================================
# Summary
# ===========================================================================

Write-Host '=== Summary ===' -ForegroundColor Cyan
$Summary | Format-Table -AutoSize -Wrap -Property @{ N = 'Profile'; E = { Split-Path -Leaf $_.Profile } }, File, Action, Detail

if (-not $Remove) {
    Write-Host 'Tag table:' -ForegroundColor Cyan
    $TagTable |
        ForEach-Object { [pscustomobject]@{ Ordinal = $_.ordinal; Key = $_.key; Name = $_.name; Color = $_.color } } |
        Sort-Object -Property @{ Expression = { [int]$_.Ordinal } } | Format-Table -AutoSize
}

Write-Host 'Profile paths:'
$Summary | Select-Object -ExpandProperty Profile -Unique | ForEach-Object { Write-Host "  $_" }

Write-Host ''
if ($WhatIfPreference) {
    Write-Host '-WhatIf was in effect: no file was created, changed or deleted.' -ForegroundColor Yellow
}
elseif ($Remove) {
    Write-Host 'Tag prefs removed. Start Thunderbird to pick up the restored settings.' -ForegroundColor Green
}
else {
    Write-Host 'Start Thunderbird to pick up the tags (user.js is read at startup).' -ForegroundColor Green
}
