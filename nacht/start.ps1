# start.ps1 - zet de nachtwerker klaar en start hem. PowerShell 5.1, ASCII only.
# Gebruik (vanuit C:\dev\zeus):  powershell -ExecutionPolicy Bypass -File nacht\start.ps1
#   -Test      proefrun: tasks.test.json, 45 minuten, geen push, geen Taakplanner
#   -EndHour   uur waarop de nacht stopt (standaard 8)
param(
    [string]$Main = "C:\dev\zeus",
    [string]$Work = "C:\dev\zeus-nacht",
    [int]$EndHour = 8,
    [switch]$Test
)
$ErrorActionPreference = "Stop"
function Say($m) { Write-Host ("[start] " + $m) }
function G([string]$dir, [string[]]$a) {
    $old = $ErrorActionPreference; $ErrorActionPreference = "Continue"   # git schrijft voortgang naar stderr
    $o = & git -C $dir @a 2>&1 | Out-String; $code = $LASTEXITCODE
    $ErrorActionPreference = $old
    return [pscustomobject]@{ code = $code; out = $o.Trim() }
}

Set-Location $Main
Say "origin ophalen"
[void](G $Main @("fetch", "origin"))
$onOrigin = G $Main @("cat-file", "-e", "origin/main:nacht/nacht.ps1")
if ($onOrigin.code -ne 0) { throw "nacht/ staat nog niet op origin/main: commit en push eerst nacht/, AGENTS.md en .gitignore." }

# --- worktree
if (-not (Test-Path (Join-Path $Work ".git"))) {
    Say "worktree maken: $Work"
    $r = G $Main @("worktree", "add", "-B", "nacht", $Work, "origin/main")
    if ($r.code -ne 0) { throw "worktree maken mislukte: $($r.out)" }
} else {
    $dirty = G $Work @("status", "--porcelain")
    if ($dirty.out -ne "") { Say "worktree heeft wijzigingen; schoonmaken (alleen de worktree, niet $Main)"; [void](G $Work @("reset", "--hard")); [void](G $Work @("clean", "-fd", "-e", "nacht/state", "-e", "nacht/logs", "-e", "nacht/tmp")) }
    [void](G $Work @("checkout", "nacht"))
    $ahead = G $Work @("rev-list", "--count", "origin/main..nacht")
    if ($ahead.out -ne "0") { Say "let op: $($ahead.out) niet-gepushte nachtcommits uit een eerdere nacht; ze worden herbruikt" }
    $rb = G $Work @("rebase", "origin/main")
    if ($rb.code -ne 0) { [void](G $Work @("rebase", "--abort")); throw "rebase van de worktree mislukte: $($rb.out)" }
}

# --- lokale (ignored) mappen en bestanden delen met de hoofdmap: data, .venv, lokale kalibratiebestanden
$exclude = Join-Path $Main ".git\info\exclude"
$ex = if (Test-Path $exclude) { Get-Content $exclude } else { @() }
foreach ($line in @("/data", "/.venv", "/local")) { if ($ex -notcontains $line) { Add-Content -Path $exclude -Value $line } }
foreach ($dir in @("data", ".venv", "local")) {
    $src = Join-Path $Main $dir; $dst = Join-Path $Work $dir
    if ((Test-Path $src) -and -not (Test-Path $dst)) {
        & cmd.exe /c mklink /J "$dst" "$src" | Out-Null
        Say "junction $dir"
    }
}
$ignored = (G $Main @("ls-files", "--others", "--ignored", "--exclude-standard", "--", "calibration", "competitions", "tactics", "schema")).out -split "`n" | ForEach-Object { $_.Trim().Trim([char]34) } | Where-Object { $_ }
foreach ($rel in $ignored) {
    $s = Join-Path $Main $rel; $d = Join-Path $Work $rel
    if ((Test-Path $s -PathType Leaf) -and -not (Test-Path $d)) {
        New-Item -ItemType Directory -Force -Path (Split-Path $d) | Out-Null
        Copy-Item $s $d
        Say "lokaal bestand gekopieerd: $rel"
    }
}

# --- venster en state voor een verse nacht
$nd = Join-Path $Work "nacht"
foreach ($sub in @("state", "tmp")) { $p = Join-Path $nd $sub; if (Test-Path $p) { Remove-Item $p -Recurse -Force } }
$now = Get-Date
$end = if ($Test) { $now.AddMinutes(45) } else { Get-Date -Hour $EndHour -Minute 0 -Second 0 -Millisecond 0 }
if (-not $Test -and $end -le $now) { $end = $end.AddDays(1) }
$win = '{ "start": "' + $now.ToString("o") + '", "end": "' + $end.ToString("o") + '" }'
[System.IO.File]::WriteAllText((Join-Path $nd "window.json"), $win, (New-Object System.Text.UTF8Encoding($false)))
Say "venster: $($now.ToString('yyyy-MM-dd HH:mm')) tot $($end.ToString('yyyy-MM-dd HH:mm'))"

# --- opstarten
$script = Join-Path $nd "nacht.ps1"
$extra = if ($Test) { " -TasksFile tasks.test.json -NoPush" } else { "" }
$arg = "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$script`"$extra"
if (-not $Test) {
    & schtasks /Create /TN ZeusNacht /SC MINUTE /MO 10 /TR "powershell.exe $arg" /F | Out-Null
    Say "Taakplanner-taak ZeusNacht: elke 10 minuten, herstart de nachtwerker als hij gestopt is"
}
Start-Process -FilePath "powershell.exe" -ArgumentList $arg -WindowStyle Hidden -WorkingDirectory $Work
Say "nachtwerker gestart. Log: $nd\logs. Stoppen: powershell -File $Main\nacht\stop.ps1"
