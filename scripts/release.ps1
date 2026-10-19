<#
.SYNOPSIS
  Eén opdracht voor een release-kandidaat (meerjarenplan 6.2): schone controle -> volledige suite -> export -> zip
  -> archiefcontrole -> exetest -> SHA256 -> (optioneel) lokale tag. Zie docs/release-check.md.
.DESCRIPTION
  Knoopt bestaande scripts aan elkaar: release_check.py (build, sim-tests, smoke, autotests, playtest, package),
  build_demo.ps1 (export zonder godot/tests, zip, pakketcontrole) en prepare_playable.ps1 (zippaden).
  Stopt bij de eerste rode stap. Uitvoer: dist/release-<zipnaam>-<tijd>/ met zip, SHA256.txt, logs/ en unpacked/.
  -AllowDirty: proefrun op een vuile werkboom; de zip krijgt '-dirty' in de naam. -Tag maakt alleen een lokale tag
  (nooit met -AllowDirty); er wordt nooit gepusht. -DryRun (alias -WhatIf) toont de stappen zonder iets te starten.
#>
param(
    [switch]$AllowDirty,
    [string]$Tag = '',
    [string]$ReleaseLabel = '',          # leeg = standaard van build_demo.ps1
    [Alias('WhatIf')][switch]$DryRun
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$py = 'python'
$check = Join-Path $PSScriptRoot 'release_check.py'
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$dirty = [bool](git -C $repo status --porcelain --untracked-files=normal)
$out = $null      # wordt na de zip gezet: dist/release-<zipnaam>-<tijd>
$zip = $null
$results = [ordered]@{}

function Native([string]$what) { if ($LASTEXITCODE -ne 0) { throw "$what (exit $LASTEXITCODE)" } }
function Suite([string]$only, [string]$exe = '') {
    $logs = if ($out) { Join-Path $out "logs\$($only -replace ',', '+')" } else { Join-Path $repo "local\release\$stamp\$($only -replace ',', '+')" }
    $a = @($check, '--only', $only, '--out', $logs)
    if ($exe) { $a += @('--exe', $exe) }
    & $py @a; Native "release_check.py --only $only"
}

$steps = @(
    @{ n = 'schone controle'; d = 'git status --porcelain leeg (anders stoppen, tenzij -AllowDirty)'; s = {
        if ($Tag -and $AllowDirty) { throw '-Tag gaat niet samen met -AllowDirty: een tag hoort bij een schone commit' }
        if ($Tag -and (git -C $repo tag -l $Tag)) { throw "tag '$Tag' bestaat al" }
        if ($dirty -and -not $AllowDirty) { git -C $repo status --short; throw 'werkboom is vuil; commit eerst, of gebruik -AllowDirty voor een proefrun' }
        if ($dirty) { Write-Host 'Vuile werkboom toegestaan (-AllowDirty): zip wordt als -dirty gemarkeerd.' -ForegroundColor Yellow } } }
    @{ n = 'build';           d = 'release_check.py --only build';  s = { Suite 'build' } }
    @{ n = 'sim-tests';       d = 'release_check.py --only test';   s = { Suite 'test' } }
    @{ n = 'smoke alle scenes'; d = 'release_check.py --only smoke'; s = { Suite 'smoke' } }
    @{ n = 'UI-autotests';    d = 'release_check.py --only autotest,autotest_expected_red'; s = { Suite 'autotest,autotest_expected_red' } }
    @{ n = 'export + zip';    d = 'build_demo.ps1 (export zonder godot/tests, pakketcontrole build/, zip) en verplaatsen naar dist/release-*'; s = {
        $before = Get-Date
        $bd = @{}; if ($ReleaseLabel) { $bd.ReleaseLabel = $ReleaseLabel }
        & (Join-Path $PSScriptRoot 'build_demo.ps1') @bd
        $made = Get-ChildItem (Join-Path $repo 'dist') -Filter 'GaffersNotebook-*.zip' -File | Where-Object { $_.LastWriteTime -ge $before } |
            Sort-Object LastWriteTime -Descending | Select-Object -First 1
        if (-not $made) { throw 'build_demo.ps1 maakte geen zip in dist/' }
        $name = $made.BaseName + $(if ($dirty) { '-dirty' } else { '' })
        $script:out = Join-Path $repo "dist\release-$name-$stamp"
        New-Item -ItemType Directory -Force $script:out | Out-Null
        $script:zip = Join-Path $script:out "$name.zip"
        Move-Item $made.FullName $script:zip } }
    @{ n = 'archiefcontrole'; d = 'prepare_playable.ps1 (paden), uitpakken + inhoud gelijk aan build/ (CRC), geen calibration/, release_check.py --only package (notices, whitelist)'; s = {
        & (Join-Path $PSScriptRoot 'prepare_playable.ps1') -ZipPath $zip
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $unp = Join-Path $out 'unpacked'
        [IO.Compression.ZipFile]::ExtractToDirectory($zip, $unp)
        $build = Join-Path $repo 'build'
        $files = @(Get-ChildItem $unp -Recurse -File | ForEach-Object { $_.FullName.Substring($unp.Length + 1) })
        $bad = @($files | Where-Object { $_.Replace('\', '/') -match '(^|/)calibration/' })
        if ($bad) { throw "calibration/ in zip: $($bad -join ', ')" }
        if ($files.Count -ne @(Get-ChildItem $build -Recurse -File).Count) { throw 'aantal bestanden in zip wijkt af van build/' }
        foreach ($f in $files) {   # gelijk aan de bron = geen CRC-/kopieerfout
            if ((Get-FileHash (Join-Path $unp $f)).Hash -ne (Get-FileHash (Join-Path $build $f)).Hash) { throw "inhoud wijkt af van build/: $f" }
        }
        Write-Host "Archief: $($files.Count) bestanden gelijk aan build/, geen calibration/"
        Suite 'package' (Join-Path $unp 'GaffersNotebook.exe') } }
    @{ n = 'exetest';         d = 'release_check.py --only playtest --exe <uitgepakte zip>\GaffersNotebook.exe'; s = { Suite 'playtest' (Join-Path $out 'unpacked\GaffersNotebook.exe') } }
    @{ n = 'SHA256';          d = 'SHA256.txt naast de zip'; s = {
        $h = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant()
        [IO.File]::WriteAllText((Join-Path $out 'SHA256.txt'), "$h  $(Split-Path $zip -Leaf)`n")
        Write-Host "SHA256 $h" } }
    @{ n = 'lokale tag';      d = $(if ($Tag) { "git tag $Tag (lokaal, niet pushen)" } else { 'overgeslagen (geen -Tag)' }); s = {
        if ($Tag) { git -C $repo tag $Tag; Native "git tag $Tag"; Write-Host "Lokale tag $Tag op $(git -C $repo rev-parse --short HEAD); niet gepusht." } } }
)

function Summary {
    Write-Host "`n== Samenvatting" -ForegroundColor Cyan
    foreach ($st in $steps) {
        $v = if ($results.Contains($st.n)) { $results[$st.n] } else { 'niet gedraaid' }
        $c = switch ($v) { 'GROEN' { 'Green' } 'ROOD' { 'Red' } default { 'DarkGray' } }
        Write-Host ("{0,-20} {1}" -f $st.n, $v) -ForegroundColor $c
    }
    if ($zip -and (Test-Path $zip)) { Write-Host "uitvoer: $out" }
}

Write-Host "repo: $repo  werkboom: $(if ($dirty) { 'VUIL' } else { 'schoon' })  HEAD: $(git -C $repo rev-parse --short HEAD)"
if ($DryRun) {
    $i = 0; foreach ($st in $steps) { $i++; Write-Host ("{0,2}. {1,-20} {2}" -f $i, $st.n, $st.d) }
    Write-Host "uitvoer: dist\release-GaffersNotebook-<label>$(if ($dirty) { '-dirty' })-<tijd>\  (proefrun: niets gestart)"
    exit 0
}
foreach ($st in $steps) {
    Write-Host "`n==== $($st.n) $(Get-Date -Format HH:mm:ss)" -ForegroundColor Cyan
    try { & $st.s; $results[$st.n] = 'GROEN' }
    catch {
        $results[$st.n] = 'ROOD'
        Write-Host "ROOD bij '$($st.n)': $($_.Exception.Message)" -ForegroundColor Red
        Summary; exit 1
    }
}
Summary
Write-Host 'Release GROEN.' -ForegroundColor Green
