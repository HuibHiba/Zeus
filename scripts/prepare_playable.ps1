<#
.SYNOPSIS
  Controleert een demopakket uit dist/. Alleen met -Playable wordt het uitgepakt naar playable/ en wordt de bureaubladsnelkoppeling bijgewerkt.
#>
param([Parameter(Mandatory=$true)][string]$ZipPath, [switch]$Playable)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$archivePath = (Resolve-Path -LiteralPath $ZipPath).Path
$distRoot = [IO.Path]::GetFullPath((Join-Path $repo 'dist')) + [IO.Path]::DirectorySeparatorChar
if (-not $archivePath.StartsWith($distRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Gebruik een pakket uit dist/.' }
$name = [IO.Path]::GetFileNameWithoutExtension($archivePath)
if ([IO.Path]::GetExtension($archivePath) -ne '.zip' -or $name -notmatch '^GaffersNotebook-[A-Za-z0-9-]+$') { throw 'Onverwachte pakketnaam.' }
$playableRoot = Join-Path $repo 'playable'
$destination = [IO.Path]::GetFullPath((Join-Path $playableRoot $name))
$prefix = $destination + [IO.Path]::DirectorySeparatorChar
$archiveHash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
$hashFile = Join-Path $destination 'ARCHIVE_SHA256.txt'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead($archivePath)
try {
    $names = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    foreach ($entry in $archive.Entries) {
        if ($entry.FullName.Contains('\') -or $entry.FullName.Contains(':') -or -not $names.Add($entry.FullName)) { throw 'Onveilig of dubbel pakketpad.' }
        $target = [IO.Path]::GetFullPath((Join-Path $destination $entry.FullName))
        if (-not $target.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Pakketpad valt buiten de speelmap.' }
    }
    foreach ($required in @('GaffersNotebook.exe', 'VERSIE.txt', 'LEESMIJ.txt', 'data/lang/en.json', 'data/lang/nl.json')) {
        if (-not $names.Contains($required)) { throw "Pakketbestand ontbreekt: $required" }
    }
} finally { $archive.Dispose() }
if (-not $Playable) {
    Write-Host "Pakket gecontroleerd: $archivePath"
    Write-Host 'Niet uitgepakt en bureaubladsnelkoppeling ongewijzigd; gebruik -Playable om dat wel te doen.'
    return
}
if (Test-Path -LiteralPath $destination) {
    if (-not (Test-Path -LiteralPath $hashFile) -or (Get-Content -LiteralPath $hashFile -Raw).Trim() -ne $archiveHash) {
        throw 'Deze speelmap bestaat al met een andere inhoud; de bestaande versie blijft behouden.'
    }
} else {
    [IO.Compression.ZipFile]::ExtractToDirectory($archivePath, $destination)
    [IO.File]::WriteAllText($hashFile, $archiveHash)
}
$exe = Join-Path $destination 'GaffersNotebook.exe'
$check = Join-Path $destination '.startup-check'
New-Item -ItemType Directory -Force -Path $check | Out-Null
$previousAppData = $env:APPDATA
try {
    $env:APPDATA = Join-Path $check 'Roaming'
    New-Item -ItemType Directory -Force -Path $env:APPDATA | Out-Null
    $process = Start-Process -FilePath $exe -ArgumentList @('--headless', '--quit-after', '40') -WorkingDirectory 'C:\Windows' -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $check 'stdout.txt') -RedirectStandardError (Join-Path $check 'stderr.txt')
    if (-not $process.WaitForExit(30000)) {
        $process.Kill()
        throw 'De startcontrole reageerde niet; de vorige snelkoppeling blijft behouden.'
    }
    $process.WaitForExit()
    $errors = Get-Content -LiteralPath (Join-Path $check 'stderr.txt') -Raw
    if ($process.ExitCode -ne 0 -or $errors -match '(?m)^(ERROR:|SCRIPT ERROR:|Unhandled exception)') {
        throw "De startcontrole faalde; de vorige snelkoppeling blijft behouden. $errors"
    }
} finally { $env:APPDATA = $previousAppData }
$desktop = [Environment]::GetFolderPath('DesktopDirectory')
if (-not $desktop -or -not (Test-Path -LiteralPath $desktop)) { throw 'Het echte Windows-bureaublad is niet beschikbaar.' }
$shortcutPath = Join-Path $desktop "Gaffer's Notebook.lnk"
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $exe
$shortcut.WorkingDirectory = $destination
$shortcut.IconLocation = "$exe,0"
$shortcut.WindowStyle = 1
$shortcut.Description = "Gaffer's Notebook - nieuwste gecontroleerde speelbuild"
$shortcut.Save()
$version = (Get-Content -LiteralPath (Join-Path $destination 'VERSIE.txt') -Raw).Trim()
@{ version = $version; archive = $archivePath; archiveSha256 = $archiveHash; executable = $exe; shortcut = $shortcutPath } |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $playableRoot 'latest.json') -Encoding UTF8
Write-Host "Speelversie: $exe"
Write-Host "Bureaublad: $shortcutPath"
