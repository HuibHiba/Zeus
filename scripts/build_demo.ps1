<#
.SYNOPSIS
  Bouwt de Windows-demo (zip) van Gaffer's Notebook. Zie docs/demo.md, sectie 'Export en zip'.
.DESCRIPTION
  1. controleert Godot-exporttemplates en geheugen, 2. schrijft build/VERSIE.txt, 3. dotnet build + Godot headless export,
  4. kopieert alleen toegestane speldata naar build/data/, 5. schrijft build/LEESMIJ.txt, 6. controleert het pakket (check_release_content.py --package), 7. maakt dist/GaffersNotebook-<ReleaseLabel>.zip.
  Pas GodotExe aan als Godot elders staat. Schrijft nooit buiten build/ en dist/ (behalve de Godot-gebruikersmap bij het exporteren).
  GodotExe: parameter, anders omgevingsvariabele GODOT_EXE, anders de WinGet-installatie (Godot Mono, console-exe).
  Met -Playable zet het script de build ook klaar in playable/ en werkt het de bureaubladsnelkoppeling bij (standaard niet).
#>
param(
    [string]$GodotExe = '',
    [string]$TemplatesVersion = '4.7.2.stable.mono',
    [double]$MinFreeGb = 3.0,
    [switch]$SkipMemoryCheck,
    [switch]$Playable,
    [string]$ReleaseLabel = '0.1.0-beta.1'
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$build = Join-Path $repo 'build'
$dist = Join-Path $repo 'dist'
$exe = Join-Path $build 'GaffersNotebook.exe'

function Step($t) { Write-Host "`n== $t" -ForegroundColor Cyan }

# Godot-pad: parameter, dan GODOT_EXE, dan de WinGet-installatie.
if (-not $GodotExe) { $GodotExe = $env:GODOT_EXE }
if (-not $GodotExe) {
    $winget = Join-Path $env:LOCALAPPDATA 'Microsoft\WinGet\Packages'
    if (Test-Path $winget) {
        $found = Get-ChildItem -Path $winget -Recurse -Filter 'Godot_v*_mono_win64_console.exe' -File -ErrorAction SilentlyContinue |
            Sort-Object FullName -Descending | Select-Object -First 1
        if ($found) { $GodotExe = $found.FullName }
    }
}
if (-not $GodotExe -or -not (Test-Path -LiteralPath $GodotExe)) {
    throw "Godot-console-exe niet gevonden. Zet de omgevingsvariabele GODOT_EXE (pad naar Godot_v4.7.2-stable_mono_win64_console.exe), geef -GodotExe op, of installeer Godot Mono via WinGet. Gezocht: '$GodotExe'"
}

# (a) templates, Godot en geheugen
Step 'Controle templates en omgeving'
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
$dirty = (git -C $repo status --porcelain --untracked-files=normal) -ne $null -and (git -C $repo status --porcelain --untracked-files=normal).Length -gt 0
if ($dirty) { $hash += '-dirty' }
$buildTag = Get-Date -Format 'yyyyMMdd-HHmmss'
$stamp = "$ReleaseLabel ($hash $buildTag)"   # GameInfo toont alleen het label, de hash staat hier en in About
[IO.File]::WriteAllText((Join-Path $build 'VERSIE.txt'), $stamp + "`n", (New-Object Text.UTF8Encoding($false)))
Write-Host "Versie: $stamp"

# (c) bouwen en exporteren
Step 'dotnet build'
dotnet build (Join-Path $repo 'godot\Zeus.Godot.csproj') --nologo -v q -p:UseSharedCompilation=false -nodeReuse:false
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
$previousSharedCompilation = $env:UseSharedCompilation
$env:UseSharedCompilation = 'false'
try {
    $ErrorActionPreference = 'Continue'   # Godot schrijft op stderr; dat is geen scriptfout
    $log = & $GodotExe --headless --path (Join-Path $repo 'godot') --export-release 'Windows Desktop' $exe 2>&1 | ForEach-Object { "$_" }
    $code = $LASTEXITCODE
    $ErrorActionPreference = 'Stop'
} finally {
    $env:UseSharedCompilation = $previousSharedCompilation
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
$tracked = git -C $repo ls-files -- tactics squads challenges data/lang data/tutorial   # competitions/ niet: echte competitienamen, het spel laadt het niet
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

# Licentieteksten uit dezelfde enginebinary als de export, inclusief ingebouwde componenten.
Step 'Godot-licentieteksten'
$ErrorActionPreference = 'Continue'   # Godot schrijft op stderr (bv. "ObjectDB instances were leaked"); dat is geen scriptfout
& $GodotExe --headless --path (Join-Path $repo 'godot') --script (Join-Path $repo 'scripts\export_notices.gd') -- (Join-Path $build 'GODOT_LICENSES.txt') 2>&1 | Out-Host
$ErrorActionPreference = 'Stop'
if ($LASTEXITCODE -ne 0 -or -not (Test-Path (Join-Path $build 'GODOT_LICENSES.txt'))) { throw 'Godot-licentieteksten ontbreken' }

# De zelfstandige .NET-runtime wordt meegeleverd; neem de notices van exact die versie over.
$runtimeConfigs = @(Get-ChildItem $build -Recurse -Filter '*.runtimeconfig.json' -File)
$runtimeVersions = @($runtimeConfigs | ForEach-Object {
    (Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json).runtimeOptions.includedFrameworks |
        Where-Object { $_.name -eq 'Microsoft.NETCore.App' } | ForEach-Object { $_.version }
} | Select-Object -Unique)
if ($runtimeVersions.Count -ne 1) { throw 'Geen eenduidige zelfstandige .NET-runtimeversie in export' }
$assets = Get-Content (Join-Path $repo 'godot\.godot\mono\temp\obj\project.assets.json') -Raw | ConvertFrom-Json
$runtimePackage = $null
foreach ($folder in $assets.packageFolders.PSObject.Properties.Name) {
    $candidate = Join-Path $folder ('microsoft.netcore.app.runtime.win-x64\' + $runtimeVersions[0])
    if (Test-Path (Join-Path $candidate 'THIRD-PARTY-NOTICES.TXT')) { $runtimePackage = $candidate; break }
}
if (-not $runtimePackage) { throw 'Licenties van de meegeleverde .NET-runtime niet gevonden' }
Copy-Item (Join-Path $runtimePackage 'LICENSE.TXT') (Join-Path $build 'DOTNET_LICENSE.txt')
Copy-Item (Join-Path $runtimePackage 'THIRD-PARTY-NOTICES.TXT') (Join-Path $build 'DOTNET_THIRD_PARTY_NOTICES.txt')
Copy-Item (Join-Path $repo 'LICENSES\WYSCOuT_NOTICE.txt') (Join-Path $build 'WYSCOuT_NOTICE.txt')
Copy-Item (Join-Path $repo 'LICENSES\BRONNEN_NOTICE.txt') (Join-Path $build 'BRONNEN_NOTICE.txt')
Copy-Item (Join-Path $repo 'LICENSES\MUSIC_CREDITS.txt') (Join-Path $build 'MUSIC_CREDITS.txt')
Copy-Item (Join-Path $repo 'godot\assets\fonts\OFL.txt') (Join-Path $build 'FONT_OFL.txt')

# (e) LEESMIJ
Step 'LEESMIJ.txt'
$readme = @"
GAFFER'S NOTEBOOK - early demo / work in progress / vroege demo, werk in uitvoering
Version / Versie: $stamp
==========================================================================

ENGLISH
-------
This is an early demo and work in progress. AI and balance are still being tested.
Engine and component licenses: GODOT_LICENSES.txt; https://godotengine.org/license/
.NET runtime licenses: DOTNET_LICENSE.txt and DOTNET_THIRD_PARTY_NOTICES.txt.
Parts of the match model are derived from the public Wyscout soccer match event dataset
(Pappalardo et al., CC BY 4.0); changes were made. Details: WYSCOuT_NOTICE.txt.
Default tactic line heights are informed by averages computed from SkillCorner Open Data
(https://github.com/SkillCorner/opendata, credit: SkillCorner) and from Bassek et al., DFL/Sportec Solutions
(CC BY 4.0, changed by us; not endorsed by the authors or the DFL). No raw data is included.
Sources, licenses and links for DFL and SkillCorner: BRONNEN_NOTICE.txt.
Some game parameters were tuned with the help of StatsBomb Open Data (credit: StatsBomb, https://github.com/statsbomb/open-data).
Audio: sound effects are generated for this game. Music: Kevin MacLeod (CC BY 4.0) and Erik Satie (public domain, recording by Robin Alciatore via Musopen); credits in MUSIC_CREDITS.txt.
Barlow, Big Shoulders Display, IBM Plex Sans and Kalam fonts: FONT_OFL.txt (SIL Open Font License 1.1).

START
1. Extract the whole zip to a normal folder (right click > Extract All).
   Do not run the game from inside the zip.
2. Start GaffersNotebook.exe. No installation needed (no Godot, .NET or Python).
3. Keep the "data" folder next to GaffersNotebook.exe; the game needs it.

MATCH LENGTH
The standard pace plays all 90 football minutes in about 4 to 5 real minutes, plus pauses.
Press Esc for Settings > Game > Match length: standard = about 5 minutes,
relaxed = about 8 minutes, tactical study = slower. Space pauses; B opens coaching at the next break.
End calculates the remaining match quickly. These controls also apply to live challenges and league matches.

WINDOWS WARNINGS
The game is not code-signed, so Windows SmartScreen may say "Windows protected your PC".
Click "More info", then "Run anyway". Some antivirus programs may also warn about or block
an unknown program. Only run this copy if you trust its source; you can choose not to run it.
The game does not use the internet and sends nothing automatically.

IF SOMETHING GOES WRONG
Press F9, or choose Esc > Help and feedback. Add what happened and what you expected.
The game makes one zip on your desktop (or in the user folder if the desktop is unavailable)
with a screenshot, version/system details, logs and progress. Nothing is sent automatically.
Feedback goes to one place: the comments on the itch.io page of the game. Describe the problem
there; the maker asks for the zip privately if needed. Do not post the feedback zip publicly:
it contains logs, system details and your progress.

YOUR PROGRESS
Your tactics, progress and settings are saved in %APPDATA%\GaffersNotebook\ (paste this in
the Explorer address bar). For a new version: extract it to a NEW folder; your progress stays
because it is not in the game folder. Back up this folder before an update. Saving keeps the
previous version as .bak; damaged files are retained as .bad. See the maker if recovery is needed.

HELP AND SHARING
F1 or ? explains the current screen. The welcome checklist helps with your first session.
In the Tactic maker, Open > Export file shares a JSON file; Open > Import file opens a friend's.
Save an imported tactic to keep your own copy. Cancel stops the meter or challenge simulations.

KNOWN LIMITATIONS
- Early version; things will change, and old progress may stop working after an update.
- English and Dutch are available in Esc > Game > Language; technical logs may use English.
- Keyboard and mouse only; no gamepad.
- Windows 10/11 64-bit with an OpenGL 3.3-capable graphics card (Vulkan is not required).
- Matches are simulations and can show odd behaviour; results are not balanced yet
  (especially on amateur level).
- Not tested on every pc or on a clean Windows installation without development tools.

NEDERLANDS
----------
Dit is een vroege demo en werk in uitvoering. AI en balans worden nog getest.
Licenties van engine en componenten: GODOT_LICENSES.txt; https://godotengine.org/license/
Licenties van de .NET-runtime: DOTNET_LICENSE.txt en DOTNET_THIRD_PARTY_NOTICES.txt.
Delen van het wedstrijdmodel zijn afgeleid van de openbare Wyscout-dataset met wedstrijdevents
(Pappalardo e.a., CC BY 4.0); er zijn wijzigingen aangebracht. Details: WYSCOuT_NOTICE.txt.
Standaard lijnhoogtes van tactieken zijn mede gebaseerd op gemiddelden uit SkillCorner Open Data
(https://github.com/SkillCorner/opendata, credit: SkillCorner) en uit Bassek e.a., DFL/Sportec Solutions
(CC BY 4.0, door ons bewerkt; niet goedgekeurd door de auteurs of de DFL). Er zijn geen ruwe data bijgesloten.
Bronnen, licenties en links voor DFL en SkillCorner: BRONNEN_NOTICE.txt.
Enkele spelparameters zijn afgesteld met behulp van StatsBomb Open Data (credit: StatsBomb, https://github.com/statsbomb/open-data).
Audio: geluidseffecten zijn voor dit spel gegenereerd. Muziek: Kevin MacLeod (CC BY 4.0) en Erik Satie (publiek domein, opname van Robin Alciatore via Musopen); vermeldingen in MUSIC_CREDITS.txt.
Lettertypen Barlow, Big Shoulders Display, IBM Plex Sans en Kalam: FONT_OFL.txt (SIL Open Font License 1.1).

STARTEN
1. Pak de hele zip uit naar een gewone map (rechtermuisknop > Alles uitpakken).
   Start het spel niet vanuit de zip.
2. Start GaffersNotebook.exe. Installeren is niet nodig (geen Godot, .NET of Python).
3. Laat de map "data" naast GaffersNotebook.exe staan; het spel heeft die nodig.

WEDSTRIJDDUUR
Standaard worden alle 90 speelminuten in circa 4 tot 5 echte minuten gespeeld, plus pauzes.
Kies via Esc > Spel > Wedstrijdduur: standaard = circa 5 minuten, rustig = circa 8 minuten,
tactisch analyseren = langzamer. Spatie pauzeert; B opent coachen bij de volgende onderbreking.
End berekent de resterende wedstrijd snel. Dit geldt ook voor live-uitdagingen en competitiewedstrijden.

WINDOWS-WAARSCHUWINGEN
Het spel is niet ondertekend, dus Windows SmartScreen kan "Windows heeft uw pc beveiligd" melden.
Klik op "Meer info" en dan op "Toch uitvoeren". Sommige antivirusprogramma's waarschuwen of
blokkeren ook een onbekend programma. Start deze kopie alleen als je de afzender vertrouwt;
je kunt ervoor kiezen niet te spelen. Het spel gebruikt geen internet en verstuurt niets automatisch.

ALS ER IETS MISGAAT
Druk op F9 of kies Esc > Hulp en feedback. Beschrijf wat er gebeurde en wat je verwachtte.
Het spel maakt een zip op je bureaublad (of in de gebruikersmap als het bureaublad niet beschikbaar
is) met een screenshot, versie-/systeemgegevens, logs en voortgang. Er wordt niets automatisch verstuurd.
Feedback gaat via een kanaal: de reacties op de itch.io-pagina van het spel. Beschrijf daar het
probleem; de maker vraagt de zip zo nodig apart op. Plaats de feedbackzip niet openbaar:
die bevat logs, systeemgegevens en je voortgang.

JE VOORTGANG
Je tactieken, voortgang en instellingen staan in %APPDATA%\GaffersNotebook\ (plak dit in de
adresbalk van Verkenner). Bij een nieuwe versie: pak die uit in een NIEUWE map; je voortgang
blijft bestaan omdat die niet in de spelmap staat. Maak voor een update een kopie van deze map.
Opslaan bewaart de vorige versie als .bak; beschadigde bestanden blijven als .bad bewaard.
Vraag de maker om hulp als terugzetten nodig is.

HULP EN DELEN
F1 of ? legt het huidige scherm uit. De welkomstchecklist helpt je op weg.
In de Tactiekmaker deel je via Open > Bestand exporteren een JSON-bestand; Bestand importeren
opent de tactiek van een vriend. Sla een import op om je eigen kopie te bewaren.
Annuleren stopt een berekening in de meter of bij uitdagingen.

BEKENDE BEPERKINGEN
- Vroege versie; er verandert nog veel, en oude voortgang kan na een update onbruikbaar worden.
- Engels en Nederlands staan onder Esc > Spel > Taal; technische logs kunnen Engels bevatten.
- Alleen toetsenbord en muis; geen gamepad.
- Windows 10/11 64-bit met een OpenGL 3.3-geschikte videokaart (Vulkan is niet nodig).
- Wedstrijden zijn simulaties en kunnen vreemd gedrag tonen; uitslagen zijn nog niet in balans
  (vooral op amateurniveau).
- Niet op elke pc of op een schone Windows-installatie zonder ontwikkeltools getest.
"@
[IO.File]::WriteAllText((Join-Path $build 'LEESMIJ.txt'), ($readme -replace "`r?`n", "`r`n"), (New-Object Text.UTF8Encoding($false)))

# (e2) pakketcontrole: whitelist, verboden extensies, paden/loginnaam/e-mail, notices, versieformaat (harde fout)
Step 'Pakketcontrole'
python (Join-Path $PSScriptRoot 'check_release_content.py') --package $build
if ($LASTEXITCODE -ne 0) { throw 'Pakketcontrole mislukt (zie hierboven)' }

# (f) zip
Step 'Zip'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = Join-Path $dist "GaffersNotebook-$ReleaseLabel.zip"   # hash en bouwtijd staan in VERSIE.txt
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
& (Join-Path $PSScriptRoot 'prepare_playable.ps1') -ZipPath $zip -Playable:$Playable
dotnet build-server shutdown | Out-Null
Write-Host 'Klaar.' -ForegroundColor Green
