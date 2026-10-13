# stop.ps1 - stopt de nachtwerker en zijn agents, en verwijdert de Taakplanner-taak. PowerShell 5.1, ASCII only.
# Werk dat al gecommit is blijft staan; een poging die nog bezig was laat wijzigingen achter in de worktree C:\dev\zeus-nacht
# (die worden bij de volgende start opgeruimd). Geen enkele wijziging in C:\dev\zeus zelf.
param([string]$Work = "C:\dev\zeus-nacht")
& schtasks /Delete /TN ZeusNacht /F 2>&1 | Out-Null
Write-Host "[stop] Taakplanner-taak ZeusNacht verwijderd"
$lock = Join-Path $Work "nacht\nacht.lock"
if (Test-Path $lock) {
    $pidText = Get-Content $lock | Select-Object -First 1
    $procId = 0
    if ([int]::TryParse($pidText, [ref]$procId) -and $procId -gt 0) {
        & taskkill /PID $procId /T /F 2>&1 | Out-Null
        Write-Host "[stop] proces $procId (met agents) gestopt"
    }
    Remove-Item $lock -Force -ErrorAction SilentlyContinue
} else {
    Write-Host "[stop] geen lockbestand: de nachtwerker draaide niet"
}
