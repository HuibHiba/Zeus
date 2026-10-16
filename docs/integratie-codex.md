# Gecombineerde aanvullingen voor de vriendendemo

Branch `integratie/demo-codex`, werkmap `C:\dev\zeus-codex-integratie`, combineert Claude's `integratie/demo-v1` op `1bccc98` met:

- `feat/demo-tactic-sharing`: tactiekbestanden importeren/exporteren.
- `feat/demo-cancellable-batches`: annuleerknoppen, rekenbudget en voortgang voor voorbereiding.
- `feat/demo-save-updates`: versieherkenning, reservekopieën en herstelbestanden.

Alle oorspronkelijke featurecommits zijn via merges behouden. Alleen de nieuwe toevoegingen bovenaan de taaltabellen en hervatkaart botsten; beide kanten zijn behouden. Editor, Challenges, Meter en de simulatorcode zijn door git gecombineerd. De hulpteksten verwijzen nu ook naar delen en annuleren.

Claude werkt ondertussen aan de niveaucorrectie en de verdere release-integratie. Deze map is bedoeld als integratie van de drie aanvullende taken, niet als een tweede releaseproces. Main en Claude's werkmap zijn niet gewijzigd. De niveaucorrectie ontbreekt hier nog.

## Geteste interacties

Een tijdelijke headless Godot-scène heeft de volgende echte functies en signalen uitgevoerd, met aparte APPDATA en feedbackmap onder `.tmp_*`:

- Ongeldig tactiekbestand importeren behoudt de actieve tactiek en onopgeslagen wijzigingen.
- Geldige import vraagt bevestiging; annuleren behoudt het model; bevestigen opent een onopgeslagen kopie.
- Export en beide bestandsdialogen werken na de merges; export verandert de onopgeslagen status niet.
- Vertrouwdheid trainen, opslaan en opnieuw laden behoudt de voortgang.
- Een carrière met domeinversie 1 wordt door de nieuwe parser gemigreerd; training, opslaan en opnieuw laden werken met de nieuwe bestandslaag; de backup bevat de oorspronkelijke oude inhoud.
- De onboarding bewaart de checklist via de nieuwe opslaglaag.
- Een feedbackzip bevat de nieuwe carrièreopslag.

De tijdelijke scène is na uitvoering verwijderd. Lokaal bewijs: `.tmp_checks/integration.log` (`INTEGRATIONCHECK PASS`), `.tmp_integration_feedback/` (feedbackzip) en `.tmp_integration_appdata/` (uitsluitend proefopslag). Er is geen feedback verstuurd. Deze controle gebruikt functies/signalen; muisbediening en layout op verschillende schermen blijven aparte controles.

## Controle op een klein venster

De import- en exportdialogen en de lopende meter/uitdagingen zijn met echte OpenGL-opnamen bekeken bij 1100×620, in Engels en Nederlands. Alle acht processen sloten zelf met exit 0; hun foutlogs zijn leeg. De annuleerknoppen en voortgangsbalken passen. De uitgebreidere Nederlandse editorhulp is ook bekeken en blijft binnen het dialoogvenster. De doelen bij uitdagingen staan in het bestaande scrollgebied.

De bestandskiezer start nu in Documenten (of de gebruikersmap als Documenten niet bestaat), in plaats van de programmamap. Deze controle opent alleen de map; er is niets in Documenten opgeslagen. Opnamen en logs staan lokaal onder `.tmp_checks/{en,nl}_small_*.png` en `nl_help_editor.png`. Godots ingebouwde labels zoals Path/Favorites/Files blijven in deze maatwerkdialoog Engels; de eigen titels, filters en actieknoppen volgen de speltaal. Dat blijft een kleine lokalisatiebeperking voor de Nederlandse versie.

## Opnemen in de release

Voeg **deze integratiebranch** samen met Claude's uiteindelijke demo-integratie; de drie losse featurebranches hoeven dan niet opnieuw. Controleer na eventuele niveaucorrectie de tests en rooktest opnieuw voor gewijzigde code. Bouw pas daarna de definitieve zip. De huidige zip in `zeus-export/dist` bevat deze aanvullingen niet.

Nog open voor de einddemo: Claude's niveaucorrectie en volledige testsuite, nieuwe release-export met de gezamenlijke code, een volledige wedstrijd/uitdaging in die export, opslag/herstart en taalwissel, en een proef op een andere Windows-pc. De maker en vrienden doen de menselijke doorspeelronde; automatische controles vervangen die niet.
