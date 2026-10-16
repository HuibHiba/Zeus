# Wissels en live bijsturen (v0)

PRD-uitgangspunt: pauzeren en bijsturen, geen realtime ingrijpen. De simulatie accepteert daarom wissels en tactiekaanpassingen alleen op een **rustmoment**. Alle aantallen en gedragingen zijn v0-aannames, geen kalibratie of realismeclaim. Code: `Match.Commands.cs` (gedrag), `MatchCommands.cs` (typen), `Replay.cs` (invoertijdlijn).

## Rustmoment en foutafhandeling

`Match.IsRestMoment` is waar vóór de eerste stap (tick 0) en zolang `Status == Restart` (inworp, hoekschop, doelschop, vrije trap, strafschop, aftrap na een doelpunt en de pauze tussen de helften, want die loopt via een hervatting). Tijdens vrij spel, na het eindsignaal en bij een gestaakte wedstrijd wordt een commando **direct geweigerd** met een `MatchCommandException` (Nederlandse, duidelijke tekst). Er is bewust **geen wachtrij** (eenvoudigste keuze; de aanroeper, bijvoorbeeld Godot, wacht tot `IsRestMoment` waar is of laat de speler pauzeren op het volgende rustmoment). Een geweigerd commando verandert niets aan de wedstrijd (alle controles gebeuren vóór de eerste wijziging; getest met toestandshash voor en na) en wordt niet in de commandolijst opgenomen.

## Wissels: `Match.Substitute(Side team, string outPlayerId, string inPlayerId)`

- Ids zijn **selectie-ids**: een basisspeler heeft als id zijn tactiekslot (`RB`, `GK`, ...), een reserve `B1`..`B7` (zie `Squad.cs`). Zonder meegegeven selectie gebruikt de wedstrijd `Squad.NeutralFor` (reserve `B1` is keeper).
- De invaller neemt het **tactiekslot** over: zelfde positie-id in snapshots en events, dezelfde positie, hetzelfde tactiekgedrag (rol, instructies, dekking). Hij start met `FatigueState.Fresh` (conditie 1,0), zonder gele kaart, met zijn eigen attributen en een eigen RNG-stroom (afgeleid van seed, wisselnummer en slot). Hij verschijnt direct op de plek van de gewisselde (geen wandeling naar de zijlijn; v0).
- Maximaal `MatchConfig.MaxSubstitutions` per team (standaard 5, 0..7). Het aantal wisselmomenten is niet beperkt.
- Een gewisselde speler kan niet terugkomen en niet nogmaals gewisseld worden. Een invaller kan zelf later weer gewisseld worden, onder zijn selectie-id (`B2`), niet onder het slot.
- Geweigerd (met uitleg): onbekend id; `out` is geen veldspeler (al gewisseld, of zit op de bank); `in` staat al op het veld, is al gewisseld of onbekend; uitgesloten speler (rood); limiet bereikt; keeper (`IsGoalkeeper`, ook een veldspeler die na een rode kaart als keeper staat) alleen door een keeper, en een keeper kan geen veldspeler vervangen.
- Event `Substitution`: `Team`, `PlayerId` = het slot, `Outcome` = `<uit>-><in>` (selectie-ids), op de huidige tick. Hulpmethoden: `SubstitutionsUsed/Left`, `BenchOf`, `SquadIdOf(team, slot)`, `SquadPlayerOf(team, slot)`.
- Beperkingen: de wedstrijdklok, scoreborden en snapshots blijven slot-ids gebruiken; een UI moet `SquadIdOf` gebruiken voor namen.

## Bijsturen: `Match.Adjust(Side team, TacticAdjustment adjustment)`

`TacticAdjustment` heeft alleen optionele velden (null = ongewijzigd):

| Veld | Betekenis |
| --- | --- |
| `Tempo`, `PressingIntensity`, `LineHeight`, `Compactness` | 0..1 (eindig), zoals de loader |
| `PressingForm` | `zone`, `man` of `gemengd` |
| `Block` | `Hoog`, `Midden`, `Laag`: moet als `verdedigen_varianten` in de tactiek bestaan |
| `Instructions` | lijst `InstructionChange(PlayerId, Add, Remove, Mark)` |

- Validatie met dezelfde regels als `TacticLoader` (`ValidateTeamSettings`, `ValidatePhasePlayer`, beide publiek): bereiken, pressingvorm, `dek_man` vereist `dek` en omgekeerd, `dek` verwijst naar een bekend slot-id. Instructienamen komen uit `TacticEditorModel.AllowedInstructions`. Een ongeldige aanpassing, ook als alleen een deel ongeldig is, gooit `MatchCommandException` en past **niets** toe. Een lege aanpassing is ook een fout.
- Instructies worden toegevoegd/verwijderd in **alle fasen en blokvarianten** van die speler. `dek_man` toevoegen vereist `Mark`; `dek_man` verwijderen wist `dek`. `Mark` kan alleen een id uit het eigen rooster zijn (zelfde beperking als de loader).
- Blokhoogte: zet de blokvariant direct en **vervangt de automatische blokwissels** (stand/minuut/vermoeidheid) van dat team voor de rest van de wedstrijd, zodat de keuze niet de volgende tick wordt overschreven. Een nieuwe `Adjust` met `Block` verandert dat weer. Geen `BlockChange`-event; het `TacticChange`-event beschrijft de keuze.
- De aanpassing geldt alleen voor het gekozen team en vervangt de `Tactic` van alle spelers van dat team (ook uitgesloten). De tactiek van de tegenstander blijft ongemoeid.
- Let op: de effecten van `Tempo`, `LineHeight` en `PressingForm=man/gemengd` bestaan alleen als de opt-in schakelaars in `MatchConfig.Instructions` aanstaan (zie [instructies.md](instructies.md)); `Compactness`, `Block` en de spelerinstructies werken met de standaardconfiguratie. `Compactness` kan niet naar `null` worden teruggezet (gebruik 0).
- Event `TacticChange`: `Team`, lege `PlayerId`, `Outcome` bijvoorbeeld `tempo=0.900;compact=0.700;blok=Laag;RB:+kom_op,-blijf_achter`.

## Replayformaat (versie 2)

Elk geaccepteerd commando komt in `Match.Commands` als `MatchCommand(Tick, Team, Kind, OutPlayerId, InPlayerId, Adjustment)`; `Tick` is het aantal voltooide stappen op het moment van toepassen. `ReplayFile.Capture(..., match.Commands)` bewaart de lijst in `ReplayFile.Commands`:

- **Versie 1** (geen commando's) blijft ongewijzigd; `Capture` zonder commando's levert nog steeds versie 1, en oude replaybestanden blijven leesbaar. **Versie 2** heeft `Commands` (verplicht, op tick geordend, tick 0..`Steps`, `Kind` is `substitute` of `adjust`). Versie 1 met commando's of versie 2 zonder lijst wordt geweigerd, evenals een ongeordende of te late tijdlijn.
- `ReplayFile.Run()` herhaalt de commando's vóór de stap waarop ze gegeven zijn (en commando's ná de laatste stap na afloop), dus dezelfde seed plus dezelfde commando's geeft dezelfde eventhash (determinismetest `ReplayCommandTests`). `ReplayFile.ApplyDue(match)` is de stateless variant voor afspelers die zelf stappen (de runner en `godot/Main.cs` roepen het vóór elke `Step()` aan en voor de verificatie); aanroepen op dezelfde tick is veilig omdat de positie in de tijdlijn `match.Commands.Count` is.
- Runner: `--replay-out` neemt `match.Commands` mee. De runner heeft zelf nog geen opties om commando's te geven (alleen bibliotheek/Godot).
- Zonder commando's verandert het gedrag niet: de bestaande golden-hashes zijn ongewijzigd. De buildfingerprint van de replay blijft de assembly-MVID; een nieuwe build vraagt dus een verse replay.

## Tests

`SubstitutionTests` (12), `AdjustTests` (7) en `ReplayCommandTests` (7): wissel op rustmoment, vers en slot overnemen, ongeldige wissels zonder toestandswijziging (zesde wissel, uitgesloten speler, keeper door veldspeler en omgekeerd, onbekend id, al gewisseld), geen wissel tijdens spel of na het eindsignaal, configureerbaar maximum, bijsturen (gemiddelde verdedigingslijn laag tegenover hoog blok over vier seeds, instructies toevoegen/verwijderen, markeren, ongeldig zonder halve toepassing), en replay met commando's die de eventhash en snapshothash reproduceert na opslaan en laden.

## Open

Geen wachtrij voor commando's buiten een rustmoment; geen wandeltijd of wisselpauze; geen wisselregels per competitie (extra wissel bij verlenging, commotiewissel); de Godot-interface voor wisselen en bijsturen is nog niet gebouwd (de bibliotheek wel); geen AI-coach die zelf wisselt.

## Tegenstander wisselt, voorlopig

`OpponentSubstitutionPolicy` is een optioneel, deterministisch v0-beleid, zonder eigen toeval of verborgen wedstrijdkennis. Het is een werkhypothese, geen gekalibreerde coach. `OpponentSubstitutionConfig` heeft standaard `FatigueMinute = 60`, `EnergyThreshold = 0.65`, `AttackingMinute = 70` en `MaxSubstitutions = 3` (instelbaar 0..3). Minuten volgen de totale simulatieklok inclusief toegevoegde tijd.

- Vanaf minuut 60 kiest het beleid de veldspeler met de laagste **lange energie** onder de drempel waarvoor een passende reserve beschikbaar is. Rolgroepen: verdediging (back, centrale verdediger, wingback), middenveld (controlerend, box-to-box, aanvallend) en aanval (buitenspeler, spits, targetman, hangende spits). Bij gelijke energie beslist het slot-id; reserves worden op selectie-id gekozen, beide ordinaal.
- Bij achterstand vanaf minuut 70 krijgt een verdediger voor een aanvaller van de bank voorrang. Ook met een rode kaart geldt dit alleen bij achterstand; een uitgesloten speler wordt nooit vervangen. De noodkeeper na rood voor de keeper is eveneens uitgesloten van de keuze. Zonder beschikbare aanvalswissel blijft de vermoeidheidsregel beschikbaar.
- Hoogstens drie gebruikte wissels per ploeg, bovendien begrensd door `MatchConfig.MaxSubstitutions`; hoogstens één per ploeg per rustmoment. Het laatste `RestartAwarded`-event markeert het begin van dat moment, zodat herhaald aanroepen op opeenvolgende pauzeticks geen extra wissels geeft. Eerdere handmatige wissels tellen mee. Een lege bank, uitsluitend reservekeepers, vrij spel en een afgelopen/gestaakte wedstrijd geven geen wissel.
- Elke toepassing loopt door `Match.Substitute`: dezelfde validatie, verse energie, `Substitution`-event en opgenomen commando. **De invaller neemt het bestaande tactiekslot en gedrag over.** Een aanvaller voor een verdediger verandert dus diens spelersprofiel, maar schuift geen formatielijn en maakt geen nieuwe aanvallende positie. Dat blijft een beperking van deze voorlopige regel.

Koppeling: `ChallengeEvaluator.Play(..., opponentSubstitutions: true)` wisselt alleen de uitploeg; `Competition.PlayRound` en `PlayRest` hebben dezelfde optionele vlag en wisselen alleen clubs die niet door de speler bestuurd worden (beide kanten bij AI-tegen-AI). Alle vlaggen staan standaard uit. De Godot-menukeuze **Tegenstander wisselt** zet `GameSession.OpponentSubstitutions`; de live wedstrijd gebruikt dit voor de kant tegenover de speler, ook bij doorspoelen en direct uitspelen. De checkbox schakelt de batchberekening in de uitdaging-/competitieschermen niet om; daarvoor bestaat de bibliotheekvlag. Bij replay staat het beleid uit: `ReplayFile.Capture(..., commands: match.Commands)` en `Run` reproduceren de opgenomen wissels via de bestaande tijdlijn. Zonder vlag verandert de simulatiekern niet.

`OpponentSubstitutionTests` controleert determinisme, een volledige wedstrijd met drie wissels en replay (event- en snapshothash), limieten, één wissel per rustmoment, rolkeuze, lege bank, keeperuitsluiting, rood/noodkeeper en achterstand, rustmomenten en standaard uit. De standaard golden-tests blijven de bestaande hashes bewaken.
