# Vertrouwdheid en training (fase 6, voorlopig)

Dit is **v0, een ongekalibreerde werkhypothese**. Concepten, formules en constanten zijn voorstellen uit de sectie 'Tactisch begrip per concept' in het vaultontwerp `02 Techniek/Spelersmodel v0.md`. Er is geen realismeclaim en geen Godot-bediening gebouwd.

## Vraag van de tactiek

`TacticDemand.From(tactic)` leest alleen de tactiek; geen wedstrijdinformatie of willekeur. Alle gewichten liggen in 0..1. `aanwezig(i)` is 1 zodra een speler in een fase of verdedigingsvariant instructie i heeft, anders 0: ook een opdracht voor één specialist kan de stijl bepalen. De vraag volgt ook uit instructies waarvan de uitvoering nog een opt-in-configvlag heeft.

`laag` is 1 bij basisblok Laag, anders `1 - lijnhoogte`. `dekkers` is de fractie fase-spelerregels met een expliciet `dek`-doel, over alle opgeloste fasen en verdedigingsvarianten.

| Concept | Voorlopige vraag w |
| --- | --- |
| hoge_pressing | pressing_intensiteit × lijnhoogte |
| gegenpressing | pressing_intensiteit bij een balverlies-trigger; anders 0,5 × aanwezig(start_pressing) |
| positiespel | min(1; 2 × (1 - tempo)) × aanwezig(vrije_man_zoeken) |
| lange_bal | tempo × max(aanwezig(loop_diep); 0,8 bij een targetman, anders 0) |
| laag_blok | laag × (1 - 0,5 × pressing_intensiteit) |
| man_om_man | max(min(1; 2 × dekkers); 1 bij pressing_vorm=man, anders 0) |
| counter | laag × tempo × aanwezig(loop_diep) |

Complexiteit = `clamp(triggers/8 + afwijkingen/8 + dekkers/4 + padfractie/4; 0; 1)`. Afwijkingen zijn het gemiddelde aantal expliciete instructies per fase-spelerregel buiten de rolstandaard. De huidige rolstandaard/sjabloon heeft **geen expliciete instructies**: de bestaande breinen volgen het getekende faseanker, reageren op waargenomen balinformatie, pressen zonaal en kiezen hun bestaande balactie. De rol onderscheidt zich hier door zijn bestaande fasepositie; er is geen nieuwe bibliotheek met ideale rolposities. Padfractie telt regels met een pad. Aliassen tellen als opgeloste fasen; varianten tellen mee. K = `1 + 0,5 × complexiteit` (1..1,5).

## Speler en uitvoering

Capaciteit per concept wordt bij gebruik afgeleid uit de **ruwe** selectieattributen (0..1): `begrip = 0,4 × decisions + 0,3 × positioning + 0,3 × specialist`. Specialist is anticipation voor hoge_pressing/gegenpressing/counter, vision voor positiespel/lange_bal, positioning voor laag_blok/man_om_man. Niveau werkt indirect via de selectieattributen. Zonder selectie gebruikt een expliciet geactiveerde staat `PlayerAttributes.Neutral`. Capaciteit groeit niet in v0; `TacticalConcepts.Understanding(attributes, concept)` maakt haar opvraagbaar.

`u_c = begrip_c × (0,5 + 0,5 × vertrouwdheid_c)` en `tekort_c = max(0; w_c × K - u_c)`. Voor een triggeractie nemen we voorlopig het **maximum** tekort over de zeven concepten; de actie wordt nog niet aan één eigen concept gekoppeld. Ook bij volledige vertrouwdheid kan de capaciteit dus onvoldoende zijn.

- Extra reactietijd: `0,8 × tekort` seconden, naar boven afgerond op hele ticks (minimaal één tick bij positief tekort).
- Extra kans op missen: `0,45 × tekort`. Bestaande attribuutstraffen worden erbij opgeteld (totale miskans maximaal 0,9); de bestaande attribuutreactietijd wordt eveneens opgeteld. Bij nul tekort blijft het oude triggerpad actief.
- Een gemiste actie zet de speler voor de normale triggerduur terug op rolgedrag: expliciete instructies en het dekkingsdoel vervallen tijdelijk, pressingvorm wordt zonaal, dieptelopen vervalt en een lopend pad stopt. De getekende formatie, waarneming, balacties en zonale pressing blijven gelden. Nieuwe paden starten niet gedurende deze terugval. Een gemiste trigger is dus ook ander gedrag dan uitsluitend langzamer uitvoeren.
- Vertraging geldt ook voor triggergestuurde paden. Normale fasepaden worden zonder gemiste trigger niet bestraft. Vertraagde opdrachten aan een gewisselde of uitgesloten speler worden weggegooid. Vertraging verlengt het opdrachtvenster mee.

`Match.FamiliarityTriggers` geeft voorlopige diagnostiek per actie: team, **selectie-id**, pogingstick, tekort, gemist en geplande reactietijd (0 bij missen). Die reactietijd is de wachttijd tot de opdracht wordt geactiveerd, niet een fysiek gemeten bewegingsreactie; daarna kan het beslisinterval nog invloed hebben. Geslaagde vertraagde opdrachten leveren het bestaande `TriggerFired`-event. Deze meting voorspelt geen uitslag of winstkans.

## Staat, leren en opslag

`FamiliarityState` hoort bij één team (`TeamId`) en bevat `Players[selectie-id].Concepts[concept]` en `Learned` per speler. Begrip wordt afgeleid en niet dubbel opgeslagen. Ontbrekende concepten binnen een aanwezige speler betekenen vertrouwdheid 0; een volledig ontbrekende speler betekent **geen effect** in Match. De match gebruikt `SquadId`, dus een invaller gebruikt bijvoorbeeld B2 en erft niet de vertrouwdheid van ST. Team-id en speler-id volgen de aanroeper; de huidige selectie-ids zijn nog slotgebonden en geen carrièrebrede persoon-ids.

`ForTeam(teamId, ids, initial)` maakt een staat; `ToJson()`/`FromJson()` bewaren en valideren de waarden en het leerbudget. `Match.Create(..., homeFamiliarity: state, awayFamiliarity: other)` neemt diepe kopieën: later trainen verandert een lopende wedstrijd niet. De match schrijft geen leergroei terug.

- `AfterMatch(state, demand)`: verdeel maximaal 0,025 groei over de gebruikte concepten naar verhouding w; vermenigvuldig elke conceptgroei met `(1 - huidige vertrouwdheid)`. Bij gebrek aan budget worden alle groeibedragen evenredig verkleind. Concepten met w=0 vervagen met factor 0,995 per aanroep. Bij helemaal geen vraag vervaagt alles.
- `Train(state, concept, intensity)`: groei `0,06 × intensity × (1 - huidige vertrouwdheid)` in dat concept. Intensiteit moet eindig in 0..1 liggen. Training veroorzaakt geen vermoeidheid en laat andere concepten ongemoeid.
- Beide delen **één limiet van 0,12 bruto groei per speler per leerperiode**, over alle concepten samen. Vervaging geeft geen budget terug. Waarden blijven in 0..1. Ook opnieuw laden uit JSON behoudt het gebruikte budget.
- `BeginPeriod(hogerNummer)` opent expliciet een nieuwe leerperiode en wist alleen het gebruikte budget. Een periode is bijvoorbeeld een kalenderweek, **geen wedstrijdhelft**. Het spel/de kalender moet dit nummer beheren. Dezelfde of een oudere periode wordt geweigerd. Er is geen automatische kalender of vervaging per verstreken dag.

`AfterMatch` behandelt alle ids in de aangeboden staat als deelnemers. **Selectie van gespeelde spelers/minuten is verantwoordelijkheid van de aanroeper**; geef een deelnemersstaat door en neem de resultaten terug over als ook bankspelers in de opgeslagen teamstaat staan. Training geldt voor alle ids in de aangeboden trainingsgroep. De sim roept geen van beide automatisch aan bij het eindsignaal; daardoor is dubbel leren door snapshots of replay uitgesloten, maar moet de aanroeper een wedstrijd maar eenmaal verwerken.

Voorlopig gebruik vanuit een toepassing:

```csharp
var state = FamiliarityState.ForTeam("club-a", squad.Starters.Select(p => p.Id));
var config = new MatchConfig(EnableFamiliarity: true); // standaard false
var match = Match.Create(config, home, away, 42, homeSquad: squad, homeFamiliarity: state);
// Speel uit; verwerk eenmaal, buiten Match:
FamiliarityState.AfterMatch(state, TacticDemand.From(home));
FamiliarityState.Train(state, "positiespel", .8);
state.BeginPeriod(1); // alleen wanneer de spelkalender een nieuwe periode begint
File.WriteAllText(path, state.ToJson());
```

`ReplayFile.Capture(..., homeFamiliarity: state, awayFamiliarity: other)` bewaart kopieën van de **beginstaat**, die de aanroeper vóór leren moet vastleggen. Replays met staat gebruiken formaat 3 en kunnen tevens de commando's uit formaat 2 bevatten; `Run()` voert de staat weer in. Zonder staat blijven formaten 1/2 gelden. Runner/Godot krijgen hier geen nieuwe bedieningsopties; een eigen afspeler die zelf `Match.Create` aanroept moet de twee staten ook doorgeven.

## Compatibiliteit en controles

`EnableFamiliarity` staat standaard **uit**. Zonder staat, met een lege staat of bij ontbrekende spelers verandert de oude RNG-reeks niet. Alleen bij positief tekort voor een aanwezige speler wordt een afzonderlijke RNG-stroom aangemaakt (seed en slot/wisselnummer). Golden-hashes zijn niet aangepast. De opt-in verandert geen attribuutinstellingen.

`FamiliarityTests` toetst de vier voorbeeldstijlen, complexiteitsonderdelen, capaciteit/formule, groei/vervaging/training, gedeeld leerbudget en JSON, ongeldige invoer zonder mutatie, snapshots en eventhash zonder staat (ook met echte selectie), gemeten missers/reactietijden over vier seeds, echte uitgestelde trigger-events en terugval in de breinwaarneming, selectie-id na wisselen, invoerkopie, determinisme en replay.

Gerichte uitvoering (2026-10-16): 18 tests groen. Uitvoeringsproef: veeleisende man-om-man-tactiek met vier verdedigende triggervoorwaarden voor alle veldspelers, vier seeds, beide teams dezelfde vertrouwdheid, 3000 ticks per wedstrijd, geen selectie (dus gelijke capaciteit). Lage vertrouwdheid (0): 760 acties, 51,18% gemist, gemiddeld 0,900 s geplande reactie bij succes. Hoge vertrouwdheid (1): 870 acties, 40,80% gemist, 0,700 s. Vergelijk percentages omdat het veranderde wedstrijdverloop verschillende aantallen kansen geeft. Dit is bewijs van het bedoelde mechanisme, geen onderbouwing van de absolute kansen. De volledige eindcontrole staat in hervatten.md.

## Wat ontbreekt

Kalibratie, empirische leer-/vergeetcurven, automatische kalender/aanwezigheid/minuten, begripgroei, specifieke concepttoewijzing per trigger, collectief afstemmen tussen spelers, afstand tot een ideaal rolsjabloon, tegenstanderscouting, overdracht tussen concepten, training met vermoeidheid, UI en koppeling aan competitie. De spelkwaliteit wordt voorlopig alleen aan tactische uitvoering getoetst, niet aan doelpunten of realistische leerduur. De eerdere documentatie in spelers.md beschrijft de toestand vóór deze opt-in-uitbreiding.


## Vertrouwdheid in de competitie

Gebouwd op feat/vertrouwdheid-competitie (voorlopige werkhypothese). Season bewaart FC Zeus' eigen FamiliarityState in de carrière, los van FamiliarityStore en menuwedstrijden. Nieuwe of oude carrières krijgen bij openen van het competitiescherm een nulstaat voor alle basis- en bank-ids. Bestaande ids behouden hun groei bij tactiekwissel, promotie en het volgende seizoen; ids blijven voorlopig positieslots, geen persoonsidentiteiten.

Een leerperiode is precies een speelronde: `(Season - 1) * 10 + Round`. Trainen (concept en intensiteit laag/middel/hoog) kan eenmaal per ronde, ook na opnieuw laden. Wedstrijdleren en training delen de bestaande limiet van 0,12. Na het succesvol verwerken van de hele ronde roept Competition eenmaal AfterMatch aan, daarna BeginPeriod voor de volgende ronde. Een mislukte of geannuleerde ronde leert niet. Voor deze minimale koppeling leert de opgeslagen selectie collectief, inclusief bankspelers; er is geen minutenweging. Live gebruikt de tactiek aan het eindsignaal (geen weging van tussentijdse tactieken). De eis wordt samen met de wachtende uitslag opgeslagen, zodat hervatten dezelfde groei geeft.

Alleen FC Zeus krijgt een staat in Match. AI heeft op ieder divisieniveau een vaste referentievertrouwdheid 0,7 en is volledig vrijgesteld van de straf; die referentiewaarde wordt daarom niet als actieve Match-staat doorgegeven. AI-tegen-AI heeft EnableFamiliarity uit. Zonder staat of met expliciet uitgeschakelde config blijft het wedstrijdverloop identiek; menuwedstrijden en uitdagingen zijn ongewijzigd.

Het paneel boven het schema toont per concept de eis inclusief K, gemiddelde huidige vertrouwdheid van de basiself en een balk. De waarschuwing gebruikt het grootste TacticDemand.Deficit van de basiself (> 0,03), met de tip om eenvoudiger te spelen of te trainen. Alle nieuwe teksten staan in beide taaltabellen. CompetitionFamiliarityTests meet slechtere tactische uitvoering bij lage vertrouwdheid over vier seeds (meer gemiste opdrachten en langere reactie), geen gegarandeerd lager doelpuntentotaal. De bestaande werkhypothese is niet op winstkans of doelpunten gekalibreerd.
