# nacht.ps1 - Zeus nachtwerker. PowerShell 5.1, ASCII only.
# Voert tasks.json uit: een taak per keer, met Codex of Claude als agent, met controles (scope, tests, review) en daarna commit + push.
# Wacht bij een limiet tot de reset. Wordt elke 10 minuten door Taakplanner (ZeusNacht) herstart als het niet draait.
param(
    [string]$Root = "C:\dev\zeus-nacht",   # eigen git-worktree: raakt de werkmap van andere sessies nooit aan
    [string]$TasksFile = "tasks.json",
    [switch]$SelfTest,
    [switch]$NoPush
)
$ErrorActionPreference = "Continue"
Set-Location $Root
$N = Join-Path $Root "nacht"
$StateDir = Join-Path $N "state"; $LogDir = Join-Path $N "logs"; $TmpDir = Join-Path $N "tmp"
foreach ($d in @($StateDir, $LogDir, $TmpDir)) { New-Item -ItemType Directory -Force -Path $d | Out-Null }
$Utf8 = New-Object System.Text.UTF8Encoding($false)
$SessionLine = "Claude-Session: https://claude.ai/code/session_013iBYRGafiN3EA2VuwgHmcY"
$VaultLogDir = "C:\Users\lth\OneDrive - Christelijk College Groevenbeek\Documenten\Zeus online\05 Sessielogs"

function Write-Text($path, $text) { [System.IO.File]::WriteAllText($path, $text, $Utf8) }
function Read-Json($path) {
    if (-not (Test-Path $path)) { return $null }
    $raw = [System.IO.File]::ReadAllText($path, $Utf8)
    if ([string]::IsNullOrWhiteSpace($raw)) { return $null }
    return ($raw | ConvertFrom-Json)
}

# ---------------------------------------------------------------- venster en log
$WindowFile = Join-Path $N "window.json"
$Window = Read-Json $WindowFile
if ($null -eq $Window) {
    $start = Get-Date
    $end = Get-Date -Hour 8 -Minute 0 -Second 0 -Millisecond 0
    if ($end -le $start) { $end = $end.AddDays(1) }
    $Window = [pscustomobject]@{ start = $start.ToString("o"); end = $end.ToString("o") }
    Write-Text $WindowFile ($Window | ConvertTo-Json)
}
$WindowStart = [DateTime]::Parse($Window.start)
$WindowEnd = [DateTime]::Parse($Window.end)
$LogFile = Join-Path $LogDir ("nacht-" + $WindowStart.ToString("yyyyMMdd") + ".log")

function Log($m) {
    $line = "{0} {1}" -f (Get-Date).ToString("yyyy-MM-dd HH:mm:ss"), $m
    try { Add-Content -Path $LogFile -Value $line -Encoding UTF8 } catch { }
    Write-Host $line
}
function Time-Left { return ($WindowEnd - (Get-Date)) }
function Out-Of-Time { return ((Get-Date) -ge $WindowEnd) }

# ---------------------------------------------------------------- hulpfuncties
function Run-Proc($file, $argList, $timeoutMin, $stdinFile, $tag) {
    $out = Join-Path $TmpDir ("$tag-out.txt"); $err = Join-Path $TmpDir ("$tag-err.txt")
    Remove-Item $out, $err -Force -ErrorAction SilentlyContinue
    $sp = @{ FilePath = $file; ArgumentList = $argList; RedirectStandardOutput = $out; RedirectStandardError = $err;
             PassThru = $true; WindowStyle = "Hidden"; WorkingDirectory = $Root }
    if ($stdinFile) { $sp.RedirectStandardInput = $stdinFile }
    $p = Start-Process @sp
    $keepHandle = $p.Handle   # zonder dit blijft ExitCode leeg na WaitForExit(ms)
    $done = $p.WaitForExit([int]($timeoutMin * 60000))
    $timedOut = -not $done
    if ($timedOut) { & taskkill /PID $p.Id /T /F 2>&1 | Out-Null; Start-Sleep -Seconds 2 }
    $code = if ($timedOut) { -1 } elseif ($null -eq $p.ExitCode) { 0 } else { [int]$p.ExitCode }
    $text = ""
    foreach ($f in @($out, $err)) { if (Test-Path $f) { $text += [System.IO.File]::ReadAllText($f, $Utf8) + "`n" } }
    return [pscustomobject]@{ code = $code; timedOut = $timedOut; text = $text; out = $out; err = $err }
}

function Git([string[]]$a) {
    $o = & git -C $Root @a 2>&1 | Out-String
    return [pscustomobject]@{ code = $LASTEXITCODE; out = $o }
}

function Get-Dirty {
    # pad -> hash van de huidige inhoud ("deleted" als het bestand weg is)
    $h = @{}
    $r = Git @("status", "--porcelain", "-uall")
    foreach ($line in ($r.out -split "`n")) {
        if ($line.Length -lt 4) { continue }
        $p = $line.Substring(3).Trim().Trim('"')
        if ($p -like "* -> *") { $p = ($p -split " -> ")[1].Trim('"') }
        $full = Join-Path $Root $p
        $h[$p] = if (Test-Path $full -PathType Leaf) { (Get-FileHash -Algorithm SHA256 $full).Hash } else { "deleted" }
    }
    return $h
}

function In-Scope($path, $scope) {
    foreach ($s in $scope) { if ($path -eq $s -or $path.StartsWith($s)) { return $true } }
    return $false
}

function Revert-Paths($paths) {
    foreach ($p in $paths) {
        $tracked = (Git @("ls-files", "--error-unmatch", "--", $p)).code -eq 0
        if ($tracked) { Git @("restore", "--source=HEAD", "--staged", "--worktree", "--", $p) | Out-Null }
        else { Remove-Item -LiteralPath (Join-Path $Root $p) -Force -Recurse -ErrorAction SilentlyContinue }
    }
}

function Get-PromptText($spec) {
    # "bestand.md#T1": sectie die begint met "### TAAK T1"; anders het hele bestand
    if ($spec -match '^(.+)#(.+)$') {
        $text = [System.IO.File]::ReadAllText((Join-Path $N $Matches[1]), $Utf8)
        $id = $Matches[2]
        $parts = [regex]::Split($text, '(?m)^### TAAK ')
        foreach ($p in $parts) { if ($p.StartsWith($id + " ") -or $p.StartsWith($id + "`r") -or $p.StartsWith($id + "`n")) { return $p } }
        throw "prompt-sectie $id niet gevonden in $($Matches[1])"
    }
    return [System.IO.File]::ReadAllText((Join-Path $N $spec), $Utf8)
}

function Get-TS($id) {
    $o = Read-Json (Join-Path $StateDir "$id.json")
    if ($null -eq $o) { $o = [pscustomobject]@{ id = $id; status = "pending"; attempts = 0; note = ""; commit = ""; reviewed = ""; pushed = ""; tests = 0; start = ""; end = "" } }
    return $o
}
function Set-TS($o) { Write-Text (Join-Path $StateDir ($o.id + ".json")) ($o | ConvertTo-Json -Depth 5) }

function Get-G {
    $o = Read-Json (Join-Path $StateDir "_global.json")
    if ($null -eq $o) { $o = [pscustomobject]@{ codexUntil = ""; claudeUntil = ""; codexWaits = 0; claudeWaits = 0; baselineTests = 0 } }
    return $o
}
function Set-G($o) { Write-Text (Join-Path $StateDir "_global.json") ($o | ConvertTo-Json) }

# ---------------------------------------------------------------- limiet-herkenning
$MonthMap = @{ jan = 1; feb = 2; mar = 3; apr = 4; may = 5; jun = 6; jul = 7; aug = 8; sep = 9; oct = 10; nov = 11; dec = 12 }

function Parse-ResetTime($text) {
    $now = Get-Date
    if ($text -match '\|(\d{10})\b') { return ([DateTimeOffset]::FromUnixTimeSeconds([int64]$Matches[1])).LocalDateTime }
    if ($text -match '(?i)try again at\s+([A-Za-z]{3})[a-z]*\s+(\d{1,2})(?:st|nd|rd|th)?,?\s*(\d{4})?,?\s*(\d{1,2}):(\d{2})\s*(AM|PM)?') {
        $mon = $MonthMap[$Matches[1].ToLower()]
        $yr = if ($Matches[3]) { [int]$Matches[3] } else { $now.Year }
        $hr = [int]$Matches[4]; $mi = [int]$Matches[5]
        if ($Matches[6]) { if ($Matches[6].ToUpper() -eq "PM" -and $hr -lt 12) { $hr += 12 }; if ($Matches[6].ToUpper() -eq "AM" -and $hr -eq 12) { $hr = 0 } }
        if ($mon) { try { return (New-Object DateTime $yr, $mon, ([int]$Matches[2]), $hr, $mi, 0) } catch { } }
    }
    if ($text -match '(?i)(?:try again|retry|resets?)\s+(?:in|after)\s+(?:(\d+)\s*h(?:ours?|rs?)?)?\s*(?:and\s*)?(?:(\d+)\s*m(?:in(?:ute)?s?)?)?') {
        $h = if ($Matches[1]) { [int]$Matches[1] } else { 0 }
        $m = if ($Matches[2]) { [int]$Matches[2] } else { 0 }
        if (($h + $m) -gt 0) { return $now.AddHours($h).AddMinutes($m) }
    }
    if ($text -match '(?i)(?:try again|resets?)\s*(?:at|after)?\s*(\d{1,2})(?::(\d{2}))?\s*(am|pm)') {
        $hr = [int]$Matches[1]; $mi = if ($Matches[2]) { [int]$Matches[2] } else { 0 }
        if ($Matches[3].ToLower() -eq "pm" -and $hr -lt 12) { $hr += 12 }
        if ($Matches[3].ToLower() -eq "am" -and $hr -eq 12) { $hr = 0 }
        $t = Get-Date -Hour $hr -Minute $mi -Second 0 -Millisecond 0
        if ($t -le $now) { $t = $t.AddDays(1) }
        return $t
    }
    return $null
}

function Test-Limit($res) {
    $t = $res.text
    $tail = if ($t.Length -gt 1800) { $t.Substring($t.Length - 1800) } else { $t }
    if ($res.code -eq 0 -and $t.Length -ge 2500) { return $false }   # lange, geslaagde run: geen limietmelding
    return ($tail -match '(?i)(usage limit|rate.?limit|limit reached|hit your limit|you.ve hit|quota|too many requests|try again (in|at|after)|resets? (at|in)|\b429\b|overloaded)')
}

function Set-Limit($agent, $text) {
    $g = Get-G
    $reset = Parse-ResetTime $text
    $waits = if ($agent -eq "codex") { [int]$g.codexWaits } else { [int]$g.claudeWaits }
    if ($null -ne $reset -and $reset -gt (Get-Date) -and $reset -lt (Get-Date).AddHours(30)) {
        $until = $reset.AddMinutes(3); $how = "afgelezen"
    } else {
        $mins = @(30, 60, 90)[[Math]::Min($waits, 2)]
        $until = (Get-Date).AddMinutes($mins); $how = "geschat ($mins min)"
    }
    if ($agent -eq "codex") { $g.codexUntil = $until.ToString("o"); $g.codexWaits = $waits + 1 } else { $g.claudeUntil = $until.ToString("o"); $g.claudeWaits = $waits + 1 }
    Set-G $g
    Log "LIMIET $agent - wacht tot $($until.ToString('yyyy-MM-dd HH:mm')) ($how)"
}

function Limit-Until($agent) {
    $g = Get-G
    $s = if ($agent -eq "codex") { $g.codexUntil } else { $g.claudeUntil }
    if ([string]::IsNullOrEmpty($s)) { return $null }
    $d = [DateTime]::Parse($s)
    if ($d -le (Get-Date)) { return $null }
    return $d
}
function Clear-Waits($agent) {
    $g = Get-G
    if ($agent -eq "codex") { $g.codexWaits = 0; $g.codexUntil = "" } else { $g.claudeWaits = 0; $g.claudeUntil = "" }
    Set-G $g
}

# ---------------------------------------------------------------- agent starten
function Invoke-Agent($agent, $promptText, $tag, $timeoutMin, $readOnly) {
    $pf = Join-Path $TmpDir "$tag-prompt.txt"
    Write-Text $pf $promptText
    $env:ECC_GATEGUARD = "off"; $env:GATEGUARD_BASH_ROUTINE_DISABLED = "1"
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"; $env:DOTNET_NOLOGO = "1"; $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
    if ($agent -eq "codex") {
        $exe = (Get-Command codex.cmd -ErrorAction Stop).Source
        $last = Join-Path $TmpDir "$tag-last.txt"
        $sb = if ($readOnly) { "read-only" } else { "workspace-write" }
        $a = @("exec", "--sandbox", $sb, "-C", $Root, "--color", "never", "-o", $last, "-")
    } else {
        $exe = (Get-Command claude -ErrorAction Stop).Source
        if ($readOnly) { $a = @("-p", "--output-format", "text", "--allowedTools", "Read", "Grep", "Glob", "Bash(git diff:*)", "Bash(git log:*)", "Bash(git status:*)", "--permission-mode", "default") }
        else { $a = @("-p", "--dangerously-skip-permissions", "--output-format", "text") }
    }
    $r = Run-Proc $exe $a $timeoutMin $pf $tag
    $msg = $r.text
    if ($agent -eq "codex" -and (Test-Path $last)) { $m = [System.IO.File]::ReadAllText($last, $Utf8); if ($m.Trim().Length -gt 0) { $msg = $m } }
    $r | Add-Member -NotePropertyName message -NotePropertyValue $msg -Force
    return $r
}

function Run-Tests {
    $r = Run-Proc "dotnet" @("test", "Zeus.sln", "--nologo") 25 $null "tests"
    $passed = 0; $failed = -1
    if ($r.text -match 'Passed!\s+-\s+Failed:\s+(\d+),\s+Passed:\s+(\d+)') { $failed = [int]$Matches[1]; $passed = [int]$Matches[2] }
    elseif ($r.text -match 'Failed!\s+-\s+Failed:\s+(\d+),\s+Passed:\s+(\d+)') { $failed = [int]$Matches[1]; $passed = [int]$Matches[2] }
    $ok = ($r.code -eq 0 -and $failed -eq 0 -and $passed -gt 0)
    $tail = if ($r.text.Length -gt 2500) { $r.text.Substring($r.text.Length - 2500) } else { $r.text }
    return [pscustomobject]@{ ok = $ok; passed = $passed; failed = $failed; tail = $tail }
}

# ---------------------------------------------------------------- een poging
function Try-Task($t, $agent) {
    $ts = Get-TS $t.id
    if ([string]::IsNullOrEmpty($ts.start)) { $ts.start = (Get-Date).ToString("o") }
    $ts.status = "running"; Set-TS $ts
    $attemptNo = [int]$ts.attempts + 1
    $tag = "$($t.id)-a$attemptNo"
    Log "START taak $($t.id) ($agent) poging $attemptNo - $($t.titel)"
    Sync-Main

    $baseline = Get-Dirty
    $head0 = (Git @("rev-parse", "HEAD")).out.Trim()
    $regels = [System.IO.File]::ReadAllText((Join-Path $N "REGELS.md"), $Utf8)
    $taakTekst = Get-PromptText $t.prompt
    $scopeTxt = ($t.scope -join ", ")
    $prev = ""
    if (-not [string]::IsNullOrEmpty($ts.note) -and $attemptNo -gt 1) { $prev = "`n`n## Vorige poging mislukte`n" + $ts.note + "`nLos dit op en probeer het opnieuw.`n" }
    $wie = if ($agent -ne $t.agent) { "Je bent $agent en neemt deze taak over van $($t.agent), die tijdelijk niet beschikbaar is (limiet). Werk precies zo als de taak beschrijft." } else { "Je bent $agent." }
    $prompt = "$regels`n`n---`n`n# Taak $($t.id): $($t.titel)`n`n$wie`n`nScope (alleen hier mag je wijzigen): $scopeTxt`n`nWerkmap: $Root`n`n$taakTekst$prev"

    $timeout = if ($t.timeoutMin) { [int]$t.timeoutMin } else { 100 }
    $left = [int](Time-Left).TotalMinutes - 5
    if ($left -lt 10) { $ts.status = "pending"; Set-TS $ts; return "geen-tijd" }
    $timeout = [Math]::Min($timeout, $left)
    $res = Invoke-Agent $agent $prompt $tag $timeout $false
    Write-Text (Join-Path $LogDir "$($t.id)-a$attemptNo.txt") ($res.message)

    $after = Get-Dirty
    $ours = @()
    foreach ($k in $after.Keys) { if (-not $baseline.ContainsKey($k) -or $baseline[$k] -ne $after[$k]) { $ours += $k } }
    foreach ($k in $baseline.Keys) { if (-not $after.ContainsKey($k)) { $ours += $k } }

    if (Test-Limit $res) {
        Set-Limit $agent $res.text
        Revert-Paths ($ours | Where-Object { In-Scope $_ $t.scope })
        $ts.status = "pending"; $ts.note = ""; Set-TS $ts
        return "limiet"
    }
    Clear-Waits $agent

    $fail = ""
    if ($res.timedOut) { $fail = "time-out na $timeout minuten" }
    elseif ($res.code -ne 0) { $fail = "agent stopte met exitcode $($res.code): " + ($res.text.Substring([Math]::Max(0, $res.text.Length - 800))) }
    $head1 = (Git @("rev-parse", "HEAD")).out.Trim()
    $outside = @($ours | Where-Object { -not (In-Scope $_ $t.scope) })
    if ($head1 -ne $head0) {
        $diff = (Git @("diff", "--name-only", $head0, $head1)).out -split "`n" | Where-Object { $_ }
        $outside += @($diff | Where-Object { -not (In-Scope $_ $t.scope) })
        Log "WAARSCHUWING: agent maakte zelf commits ($head0..$head1)"
    }
    if ($fail -eq "" -and $outside.Count -gt 0) { $fail = "wijzigingen buiten scope: " + ($outside -join ", ") }
    $inScope = @($ours | Where-Object { In-Scope $_ $t.scope })
    if ($fail -eq "" -and $inScope.Count -eq 0 -and $head1 -eq $head0) { $fail = "de agent heeft niets gewijzigd" }

    $tests = $null
    if ($fail -eq "") {
        $tests = Run-Tests
        if (-not $tests.ok) { $fail = "dotnet test mislukt (failed=$($tests.failed), passed=$($tests.passed)):`n" + $tests.tail }
        else {
            $g = Get-G
            if ($tests.passed -lt [int]$g.baselineTests) { $fail = "aantal geslaagde tests daalde van $($g.baselineTests) naar $($tests.passed)" }
        }
    }
    if ($fail -eq "" -and $t.accept) {
        $acc = Run-Proc "cmd.exe" @("/c", $t.accept) 15 $null "$tag-accept"
        if ($acc.code -ne 0) { $fail = "acceptatiecommando faalde ($($t.accept)):`n" + $acc.text.Substring([Math]::Max(0, $acc.text.Length - 1500)) }
    }
    $reviewNote = "niet gevraagd"
    if ($fail -eq "" -and $t.review) {
        $rp = "Je bent reviewer. Bekijk met 'git diff HEAD' (en indien nodig 'git status') de wijzigingen in C:\dev\zeus voor taak $($t.id): $($t.titel). Toets: (1) determinisme-regels uit nacht/REGELS.md, (2) niet verzwakte of verwijderde tests, (3) geen wijzigingen buiten scope ($scopeTxt), (4) geen duidelijke logische fouten of dode code, (5) docs kloppen met de code. Antwoord met op de EERSTE regel precies 'APPROVE' of 'REJECT: <reden in een zin>' en daarna maximaal 10 regels toelichting."
        $reviewer = if ($agent -eq "codex") { "claude" } else { "codex" }
        if ($null -ne (Limit-Until $reviewer)) { $reviewer = $agent }   # de ander heeft een limiet: zelfreview is beter dan geen
        $rev = Invoke-Agent $reviewer $rp "$tag-review" 20 $true
        if (Test-Limit $rev) { Set-Limit $reviewer $rev.text; $reviewNote = "review overgeslagen ($reviewer-limiet)" }
        else {
            $first = ($rev.message -split "`n" | Where-Object { $_.Trim() } | Select-Object -First 1)
            if ($null -ne $first -and $first.Trim().ToUpper().StartsWith("REJECT")) { $fail = "review keurde af: " + $first.Trim(); $reviewNote = "afgekeurd door $reviewer" }
            elseif ($null -ne $first -and $first.Trim().ToUpper().StartsWith("APPROVE")) { $reviewNote = "goedgekeurd door $reviewer" }
            else { $reviewNote = "review onduidelijk" }
            Write-Text (Join-Path $LogDir "$($t.id)-a$attemptNo-review.txt") $rev.message
        }
    }

    if ($fail -ne "") {
        Log "MISLUKT poging ${attemptNo}: $($fail.Split("`n")[0])"
        Revert-Paths $inScope
        Revert-Paths ($ours | Where-Object { -not (In-Scope $_ $t.scope) -and -not $baseline.ContainsKey($_) })
        $ts.attempts = $attemptNo; $ts.note = $fail
        $ts.status = if ($attemptNo -ge [int]$t.maxAttempts) { "failed" } else { "pending" }
        if ($ts.status -eq "failed") { $ts.end = (Get-Date).ToString("o") }
        Set-TS $ts
        return "mislukt"
    }

    # commit (alleen onze paden) en push
    $msgText = "$($t.commit)`n`nNachtwerker: taak $($t.id), agent $agent, review: $reviewNote. $($tests.passed) tests groen.`n`n$SessionLine"
    $mf = Join-Path $TmpDir "$tag-commitmsg.txt"; Write-Text $mf $msgText
    if ($inScope.Count -gt 0) {
        foreach ($p in $inScope) { Git @("add", "--", $p) | Out-Null }
        $c = Git @("commit", "-F", $mf)
        if ($c.code -ne 0) { Log "COMMIT FOUT: $($c.out)"; Revert-Paths $inScope; $ts.attempts = $attemptNo; $ts.note = "commit mislukte: $($c.out)"; $ts.status = "pending"; Set-TS $ts; return "mislukt" }
    }
    $ts.tests = $tests.passed; $ts.reviewed = $reviewNote; $ts.agentUsed = $agent
    $g = Get-G; $g.baselineTests = [Math]::Max([int]$g.baselineTests, $tests.passed); Set-G $g
    $ts.pushed = if (Push-Main) { "ja" } else { "nee" }
    $ts.commit = (Git @("rev-parse", "--short", "HEAD")).out.Trim()
    $ts.attempts = $attemptNo; $ts.status = "done"; $ts.note = ""; $ts.end = (Get-Date).ToString("o"); Set-TS $ts
    Log "KLAAR taak $($t.id): commit $($ts.commit), $($tests.passed) tests, review $reviewNote, push $($ts.pushed)"
    return "klaar"
}

function Sync-Main {
    # nieuwste origin/main binnenhalen; de worktree is schoon, dus rebase kan altijd (eigen onge-pushte commits komen erbovenop)
    Git @("fetch", "origin") | Out-Null
    $r = Git @("rebase", "origin/main")
    if ($r.code -ne 0) { Log "sync-rebase mislukte: $($r.out.Trim().Split("`n")[0])"; Git @("rebase", "--abort") | Out-Null }
}

function Push-Main {
    if ($NoPush) { return $false }
    for ($i = 0; $i -lt 3; $i++) {
        Sync-Main
        $p = Git @("push", "origin", "HEAD:main")
        if ($p.code -eq 0) { return $true }
        Log "push mislukte (poging $($i + 1)): $($p.out.Trim().Split("`n")[0])"
        Start-Sleep -Seconds 10
    }
    return $false
}

# ---------------------------------------------------------------- taken kiezen
function Pick-Task($tasks) {
    $soonest = $null
    foreach ($t in $tasks) {
        $ts = Get-TS $t.id
        if ($ts.status -ne "pending") { continue }
        $blocked = $false
        foreach ($req in @($t.requires)) { if ($req) { if ((Get-TS $req).status -ne "done") { $blocked = $true } } }
        if ($blocked) { continue }
        if ($t.last) { if (-not (All-Settled $tasks $t.id)) { continue } }   # afsluitende taak: pas als alle andere taken klaar of dood zijn
        $agentNow = $null
        $limP = Limit-Until $t.agent
        if ($null -eq $limP) { $agentNow = $t.agent }
        elseif ($t.fallback -and $null -eq (Limit-Until $t.fallback)) { $agentNow = $t.fallback }
        if ($null -eq $agentNow) {
            $cand = $limP
            if ($t.fallback) { $limF = Limit-Until $t.fallback; if ($null -ne $limF -and $limF -lt $cand) { $cand = $limF } }
            if ($null -eq $soonest -or $cand -lt $soonest) { $soonest = $cand }
            continue
        }
        return [pscustomobject]@{ task = $t; agent = $agentNow; wait = $null }
    }
    return [pscustomobject]@{ task = $null; agent = $null; wait = $soonest }
}

function All-Settled($tasks, $exceptId) {
    foreach ($t in $tasks) {
        if ($exceptId -and $t.id -eq $exceptId) { continue }
        $s = (Get-TS $t.id).status
        if ($s -eq "pending" -or $s -eq "running") {
            # een taak waarvan een required-taak definitief faalde kan nooit meer lopen
            $dead = $false
            foreach ($req in @($t.requires)) { if ($req) { if ((Get-TS $req).status -eq "failed") { $dead = $true } } }
            if (-not $dead) { return $false }
        }
    }
    return $true
}

# ---------------------------------------------------------------- rapport
function Write-Report($tasks, $reason) {
    $date = $WindowStart.ToString("yyyyMMdd")
    $rp = Join-Path $N "rapport-$date.md"
    $sb = New-Object System.Text.StringBuilder
    [void]$sb.AppendLine("# Nachtrapport $($WindowStart.ToString('yyyy-MM-dd'))")
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("Venster: $($WindowStart.ToString('yyyy-MM-dd HH:mm')) tot $($WindowEnd.ToString('yyyy-MM-dd HH:mm')). Gestopt: $reason. Rapport geschreven: $((Get-Date).ToString('yyyy-MM-dd HH:mm')).")
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("| taak | agent | status | pogingen | commit | tests | review | gepusht |")
    [void]$sb.AppendLine("|---|---|---|---:|---|---:|---|---|")
    foreach ($t in $tasks) {
        $s = Get-TS $t.id
        [void]$sb.AppendLine("| $($t.id) $($t.titel) | $(if ($s.agentUsed) { $s.agentUsed } else { $t.agent }) | $($s.status) | $($s.attempts) | $($s.commit) | $($s.tests) | $($s.reviewed) | $($s.pushed) |")
    }
    [void]$sb.AppendLine("")
    foreach ($t in $tasks) {
        $s = Get-TS $t.id
        if ($s.status -eq "failed" -or ($s.status -eq "pending" -and $s.note)) { [void]$sb.AppendLine("## $($t.id) niet gelukt`n"); [void]$sb.AppendLine('```'); [void]$sb.AppendLine($s.note); [void]$sb.AppendLine('```') }
        $last = Get-ChildItem $LogDir -Filter "$($t.id)-a*.txt" -ErrorAction SilentlyContinue | Where-Object { $_.Name -notlike "*review*" } | Sort-Object Name | Select-Object -Last 1
        if ($last -and $s.status -eq "done") {
            [void]$sb.AppendLine("## $($t.id) verslag van de agent`n")
            [void]$sb.AppendLine([System.IO.File]::ReadAllText($last.FullName, $Utf8))
        }
    }
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("Volledig log: nacht/logs/nacht-$date.log (niet in git).")
    Write-Text $rp $sb.ToString()
    try {
        if ((Test-Path $VaultLogDir) -and -not $NoPush) {
            $vf = Join-Path $VaultLogDir ((Get-Date).ToString("yyyy-MM-dd") + ".md")
            $head = "`n## Nachtwerker (Claude + Codex), rapport`n"
            [System.IO.File]::AppendAllText($vf, $head + $sb.ToString(), $Utf8)
        }
    } catch { Log "vault-log schrijven mislukte: $_" }
    return $rp
}

function Finish($tasks, $reason, $removeSchedule) {
    Log "EINDE: $reason"
    $rp = Write-Report $tasks $reason
    # rapport + eventuele niet-gepushte commits laten landen
    if (-not $NoPush) {
        $rel = "nacht/" + (Split-Path $rp -Leaf)
        Git @("add", "--", $rel) | Out-Null
        $mf = Join-Path $TmpDir "report-commit.txt"
        Write-Text $mf ("Nachtrapport " + $WindowStart.ToString("yyyy-MM-dd") + "`n`n" + $SessionLine)
        Git @("commit", "-F", $mf) | Out-Null
        Push-Main | Out-Null
    }
    if ($removeSchedule) { & schtasks /Delete /TN ZeusNacht /F 2>&1 | Out-Null; Log "Taakplanner-taak ZeusNacht verwijderd" }
    Log "Rapport: $rp"
}

# ---------------------------------------------------------------- zelftest van de limiet-parser
if ($SelfTest) {
    $cases = @(
        @{ t = "ERROR: You've hit your usage limit. Upgrade to Pro or try again at Oct 14th, 2026 3:05 AM."; ok = $true },
        @{ t = "Claude AI usage limit reached|1792000000"; ok = $true },
        @{ t = "You've hit your limit - resets 4am (Europe/Amsterdam)"; ok = $true },
        @{ t = "rate limit exceeded, try again in 2 hours 30 minutes"; ok = $true },
        @{ t = "Alles gedaan. 12 bestanden gewijzigd."; ok = $false }
    )
    foreach ($c in $cases) {
        $r = [pscustomobject]@{ code = 1; text = $c.t }
        $lim = Test-Limit $r; $rt = Parse-ResetTime $c.t
        Log ("SELFTEST limit=$lim reset=" + $(if ($rt) { $rt.ToString("yyyy-MM-dd HH:mm") } else { "-" }) + " :: " + $c.t.Substring(0, [Math]::Min(50, $c.t.Length)))
        if ($lim -ne $c.ok) { Log "SELFTEST FOUT"; exit 1 }
    }
    Log "SELFTEST ok"; exit 0
}

# ---------------------------------------------------------------- hoofdlus
$lock = Join-Path $N "nacht.lock"
if (Test-Path $lock) {
    $old = [int](Get-Content $lock -ErrorAction SilentlyContinue | Select-Object -First 1)
    if ($old -gt 0 -and (Get-Process -Id $old -ErrorAction SilentlyContinue)) { Write-Host "draait al (pid $old)"; exit 0 }
}
Set-Content -Path $lock -Value $PID

Add-Type -Namespace Zeus -Name Power -MemberDefinition '[DllImport("kernel32.dll")] public static extern uint SetThreadExecutionState(uint esFlags);' -ErrorAction SilentlyContinue
[void][Zeus.Power]::SetThreadExecutionState([uint32]2147483649)   # ES_CONTINUOUS | ES_SYSTEM_REQUIRED: pc slaapt niet zolang dit script draait

$tasks = (Read-Json (Join-Path $N $TasksFile)).tasks
Log "START nachtwerker pid $PID, venster tot $($WindowEnd.ToString('yyyy-MM-dd HH:mm')), $($tasks.Count) taken, taakbestand $TasksFile"
$g = Get-G
if ([int]$g.baselineTests -eq 0) {
    $b = Run-Tests
    if (-not $b.ok) { Log "BASISLIJN: tests zijn nu al rood ($($b.failed) gefaald); stop. Staart:`n$($b.tail)"; Remove-Item $lock -Force; exit 2 }
    $g.baselineTests = $b.passed; Set-G $g
    Log "basislijn: $($b.passed) tests groen"
}

try {
    while ($true) {
        if (Out-Of-Time) { Finish $tasks "tijdvenster voorbij" $true; break }
        if (All-Settled $tasks) { Finish $tasks "alle taken afgehandeld" $true; break }
        $pick = Pick-Task $tasks
        if ($null -eq $pick.task) {
            if ($null -eq $pick.wait) { Finish $tasks "geen uitvoerbare taken meer" $true; break }
            $mins = [Math]::Min(5, [Math]::Max(1, ($pick.wait - (Get-Date)).TotalMinutes))
            Log ("wacht op limiet-reset tot " + $pick.wait.ToString("HH:mm"))
            Start-Sleep -Seconds ([int]($mins * 60)); continue
        }
        $outcome = Try-Task $pick.task $pick.agent
        if ($outcome -eq "geen-tijd") { Finish $tasks "te weinig tijd over voor de volgende taak" $true; break }
        if ($outcome -eq "mislukt") { Start-Sleep -Seconds 20 }
    }
}
catch {
    Log "FOUT in hoofdlus: $_ $($_.ScriptStackTrace)"
}
finally {
    Remove-Item $lock -Force -ErrorAction SilentlyContinue
}
