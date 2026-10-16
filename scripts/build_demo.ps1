<#
.SYNOPSIS
  Bouwt de Windows-demo (zip) van Gaffer's Notebook. Zie docs/demo.md, sectie 'Export en zip'.
.DESCRIPTION
  1. controleert Godot-exporttemplates en geheugen, 2. schrijft build/VERSIE.txt, 3. dotnet build + Godot headless export,
  4. kopieert alleen toegestane speldata naar build/data/, 5. schrijft build/LEESMIJ.txt, 6. maakt dist/GaffersNotebook-<hash>.zip.
  Pas GodotExe aan als Godot elders staat. Schrijft nooit buiten build/ en dist/ (behalve de Godot-gebruikersmap bij het exporteren).
#>
param(
    [string]$GodotExe = 'C:\Users\lth\AppData\Local\Microsoft\WinGet\Packages\GodotEngine.GodotEngine.Mono_Microsoft.Winget.Source_8wekyb3d8bbwe\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe',
    [string]$TemplatesVersion = '4.7.2.stable.mono',
    [double]$MinFreeGb = 3.0,
    [switch]$SkipMemoryCheck
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$build = Join-Path $repo 'build'
$dist = Join-Path $repo 'dist'
$exe = Join-Path $build 'GaffersNotebook.exe'

function Step($t) { Write-Host "`n== $t" -ForegroundColor Cyan }

# (a) templates, Godot en geheugen
Step 'Controle templates en omgeving'
if (-not (Test-Path $GodotExe)) { throw "Godot-console-exe niet gevonden: $GodotExe" }
$tpl = Join-Path $env:APPDATA "Godot\export_templates\$TemplatesVersion"
foreach ($f in 'version.txt', 'windows_release_x86_64.exe', 'windows_debug_x86_64.exe') {
    if (-not (Test-Path (Join-Path $tpl $f))) { throw "Exporttemplate ontbreekt: $tpl\$f (installeer de officiele $TemplatesVersion-templates)" }
}
if ((Get-Content (Join-Path $tpl 'version.txt') -Raw).Trim() -ne $TemplatesVersion) { throw "version.txt in $tpl komt niet overeen met $TemplatesVersion" }
if (-not $SkipMemoryCheck) {
    for ($i = 0; $i -lt 10; $i++) {
        $free = (Get-CimInstance Win32_OperatingSystem).FreePhysicalMemory / 1MB
        if ($free -gt $MinFreeGb) { break }
        Write-Host ("Vrij geheugen {0:N1} GB <= {1} GB; wacht 30 s" -f $free, $MinFreeGb)
        Start-Sleep 30
    }
    if ($free -le $MinFreeGb) { throw ("Te weinig vrij geheugen ({0:N1} GB); probeer later opnieuw" -f $free) }
}

# schone uitvoermap (alleen binnen de repo, alleen build/)
if (Test-Path $build) {
    if ((Resolve-Path $build).Path -ne (Join-Path (Resolve-Path $repo).Path 'build')) { throw 'build-pad onverwacht' }
    Remove-Item $build -Recurse -Force
}
New-Item -ItemType Directory -Force $build, $dist | Out-Null

# (b) versie
Step 'VERSIE.txt'
$hash = (git -C $repo rev-parse --short HEAD).Trim()
$dirty = (git -C $repo status --porcelain --untracked-files=no) -ne $null -and (git -C $repo status --porcelain --untracked-files=no).Length -gt 0
if ($dirty) { $hash += '-dirty' }
$stamp = "$hash $(Get-Date -Format 'yyyy-MM-dd')"
[IO.File]::WriteAllText((Join-Path $build 'VERSIE.txt'), $stamp + "`n", (New-Object Text.UTF8Encoding($false)))
Write-Host "Versie: $stamp"

# (c) bouwen en exporteren
Step 'dotnet build'
dotnet build (Join-Path $repo 'godot\Zeus.Godot.csproj') --nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'dotnet build mislukt' }

Step 'Godot export (release)'
# Godot-mono eist godot\Zeus.Godot.sln bij het exporteren; dat bestand staat niet in git, dus maken we het tijdelijk aan.
$sln = Join-Path $repo 'godot\Zeus.Godot.sln'
$madeSln = $false
if (-not (Test-Path $sln)) {
    Push-Location (Join-Path $repo 'godot')
    try {
        dotnet new sln -n Zeus.Godot | Out-Null
        dotnet sln Zeus.Godot.sln add Zeus.Godot.csproj | Out-Null
    } finally { Pop-Location }
    $madeSln = $true
}
try {
    $ErrorActionPreference = 'Continue'   # Godot schrijft op stderr; dat is geen scriptfout
    $log = & $GodotExe --headless --path (Join-Path $repo 'godot') --export-release 'Windows Desktop' $exe 2>&1 | ForEach-Object { "$_" }
    $code = $LASTEXITCODE
    $ErrorActionPreference = 'Stop'
} finally {
    if ($madeSln -and (Test-Path $sln)) { Remove-Item $sln -Force }
}
$log | Select-Object -Last 15 | ForEach-Object { Write-Host $_ }
# Godot geeft ook bij fouten vaak exit 0; daarom ook op ERROR-regels controleren.
$errors = $log | Where-Object { $_ -match '^(ERROR|SCRIPT ERROR):' }
if ($code -ne 0 -or $errors -or -not (Test-Path $exe)) { throw ("Godot-export mislukt (exit $code):`n" + ($errors -join "`n")) }
# Godot schrijft soms losse .pck/.dll naast de exe; die blijven gewoon mee in de zip.

# (d) speldata, alleen wat in git zit en niet verboden is
Step 'Speldata kopieren'
$dataOut = Join-Path $build 'data'
$deny = '(^|/)\.tmp_', '(^|/)calibration/', 'event_models', 'pass_360', 'shot_360', 'wyscout', 'statsbomb', 'fotmob', 'skillcorner', 'team_match', '\.csv$', '\.jsonl$', '\.parquet$', '\.local\.json$', '(^|/)local/'
$tracked = git -C $repo ls-files -- tactics squads challenges competitions data/lang
$copied = @()
foreach ($rel in $tracked) {
    if ($rel -notmatch '\.json$') { continue }
    if ($deny | Where-Object { $rel -match $_ }) { Write-Host "Overgeslagen (verboden patroon): $rel"; continue }
    $target = if ($rel.StartsWith('data/')) { Join-Path $build $rel } else { Join-Path $dataOut $rel }
    New-Item -ItemType Directory -Force (Split-Path $target) | Out-Null
    Copy-Item (Join-Path $repo $rel) $target
    $copied += $rel
}
foreach ($need in 'tactics', 'squads', 'challenges', 'data/lang/en.json', 'data/lang/nl.json') {
    if (-not ($copied | Where-Object { $_ -like "$need*" })) { throw "Verplichte speldata ontbreekt: $need" }
}
$copied | Group-Object { ($_ -split '/')[0] } | ForEach-Object { Write-Host ("  {0}: {1} bestanden" -f $_.Name, $_.Count) }
# eindcontrole: niets verbodens in de uitvoer
$bad = Get-ChildItem $build -Recurse -File | Where-Object { $r = $_.FullName.Substring($build.Length + 1).Replace('\', '/'); $deny | Where-Object { $r -match $_ } }
if ($bad) { throw ("Verboden bestanden in build/: " + ($bad.FullName -join ', ')) }

# (e) LEESMIJ
Step 'LEESMIJ.txt'
$readme = @"
GAFFER'S NOTEBOOK - early demo / vroege versie
Version / Versie: $stamp
==========================================================================

ENGLISH
-------
This is an EARLY version for friends to try. Expect rough edges.

1. Unzip the whole zip to a normal folder (right click > Extract All). Do not run the game from inside the zip.
2. Start GaffersNotebook.exe. No installation, Godot, .NET or Python needed.
3. Windows SmartScreen may say "Windows protected your PC" because the game is not code-signed.
   Click "More info", then "Run anyway".
4. Keep the "data" folder next to GaffersNotebook.exe; the game needs it.
5. Your own tactics and settings are saved in %APPDATA%\GaffersNotebook\ (delete that folder to reset).
6. In-game feedback (F9) will follow in a later version. For now, tell the maker directly.

Known limitations: early version; only partly translated (English/Dutch); keyboard and mouse only;
Windows 10/11 64-bit with a Vulkan-capable graphics card; matches are simulations and may show odd behaviour.

NEDERLANDS
----------
Dit is een VROEGE versie om vrienden te laten proberen. Verwacht oneffenheden.

1. Pak de hele zip uit naar een gewone map (rechtermuisknop > Alles uitpakken). Start het spel niet vanuit de zip.
2. Start GaffersNotebook.exe. Installeren, Godot, .NET of Python is niet nodig.
3. Windows SmartScreen kan "Windows heeft uw pc beveiligd" melden omdat het spel niet is ondertekend.
   Klik op "Meer info" en dan op "Toch uitvoeren".
4. Laat de map "data" naast GaffersNotebook.exe staan; het spel heeft die nodig.
5. Eigen tactieken en instellingen staan in %APPDATA%\GaffersNotebook\ (verwijder die map om te resetten).
6. Feedback in het spel (F9) volgt in een latere versie. Meld nu iets rechtstreeks bij de maker.

Bekende beperkingen: vroege versie; nog maar deels vertaald (Engels/Nederlands); alleen toetsenbord en muis;
Windows 10/11 64-bit met een Vulkan-geschikte videokaart; wedstrijden zijn simulaties en kunnen vreemd gedrag tonen.
"@
[IO.File]::WriteAllText((Join-Path $build 'LEESMIJ.txt'), ($readme -replace "`r?`n", "`r`n"), (New-Object Text.UTF8Encoding($false)))

# (f) zip
Step 'Zip'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = Join-Path $dist "GaffersNotebook-$hash.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
# Eigen lus: CreateFromDirectory schrijft op Windows PowerShell 5.1 soms backslashes in de zip; wij willen '/'.
$fs = [IO.File]::Open($zip, [IO.FileMode]::CreateNew)
$za = New-Object IO.Compression.ZipArchive($fs, [IO.Compression.ZipArchiveMode]::Create)
try {
    Get-ChildItem $build -Recurse -File | ForEach-Object {
        $entry = $_.FullName.Substring($build.Length + 1).Replace([string][char]92, '/')
        [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($za, $_.FullName, $entry, [IO.Compression.CompressionLevel]::Optimal)
    }
} finally { $za.Dispose(); $fs.Dispose() }
$bsize = (Get-ChildItem $build -Recurse -File | Measure-Object Length -Sum).Sum
Write-Host ("build/: {0:N1} MB; zip: {1:N1} MB -> {2}" -f ($bsize / 1MB), ((Get-Item $zip).Length / 1MB), $zip)

# opruimen van buildservers (houdt geheugen vrij)
dotnet build-server shutdown | Out-Null
Write-Host 'Klaar.' -ForegroundColor Green
