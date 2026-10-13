# Nachtwerker (Claude + Codex)

Werkt een takenlijst (`tasks.json`) af terwijl niemand erbij is. Elke taak heeft een primaire agent en een terugvaller:
heeft de ene een limiet, dan neemt de andere over; bij een limiet wacht het script tot de reset.

Starten (vanuit `C:\dev\zeus`):
    powershell -ExecutionPolicy Bypass -File nacht\start.ps1          echte nacht, stopt om 08:00
    powershell -ExecutionPolicy Bypass -File nacht\start.ps1 -Test    proef met twee kleine taken, geen push
Stoppen: `powershell -File nacht\stop.ps1`

Hoe het werkt
- Werkt in een eigen git-worktree `C:\dev\zeus-nacht` (branch `nacht`, op basis van `origin/main`); de werkmap van andere sessies blijft ongemoeid.
- Per poging: agent draait (Codex in de workspace-write sandbox, Claude met --dangerously-skip-permissions), daarna controleert het script scope, `dotnet test` (geen daling in aantal), eventueel een acceptatiecommando en een review door de andere agent.
- Alles groen: commit met alleen de taak-paden en `git push origin HEAD:main` (na fetch en rebase). Anders wordt de poging teruggedraaid en krijgt de volgende poging de foutmelding mee (max. 3 pogingen).
- Taakplanner-taak `ZeusNacht` herstart het script elke 10 minuten als het gestopt is (crash, herstart pc). Het script houdt de pc wakker zolang het draait.
- Einde (08:00 of alles klaar): rapport in `nacht/rapport-<datum>.md` (gecommit) en in het sessielog van de vault.

Ochtend
- `git pull --rebase --autostash` in `C:\dev\zeus`; lees het rapport. Logs staan in `C:\dev\zeus-nacht\nacht\logs` (niet in git).
- Worktree opruimen kan met `git worktree remove C:\dev\zeus-nacht --force`.
