# Opslag bij demo-updates

Instellingen, carrière, uitdagingen, speelgeschiedenis en de bestaande vertrouwdheidsopslag blijven in dezelfde gebruikersmap staan: normaal `%APPDATA%\GaffersNotebook`. Een nieuwe zip hoeft deze bestanden niet te verplaatsen. Eigen tactieken blijven in `tactieken/`; deze wijziging verandert hun uitwisselformaat niet.

`SafeFile` gebruikt nu `UserSaveFile`, een gedeelde bestandslaag met een opslagversie en reservekopie. Oude JSON-objecten zonder opslagmarkering worden gelezen. Bij de volgende geslaagde schrijfactie krijgt het bestand een extra veld `_gaffersNotebookSaveVersion` met de tekenreeks `"1"`. De lezers krijgen dit extra veld niet door; onbekende spelvelden blijven bewaard. De markering is een tekenreeks zodat ook de bestaande lezers voor instellingen en vertrouwdheid de JSON kunnen blijven lezen. De carrière houdt daarnaast zijn bestaande eigen `Version`-veld; deze laag vervangt geen domeinspecifieke migratie.

Vóór vervangen wordt de vorige bestandsinhoud als `<bestandsnaam>.bak` bewaard. Een volgende geslaagde update vervangt deze reservekopie; er wordt één vorige versie bewaard. Mislukt schrijven of vervangen van de backup, dan wordt het hoofdbestand niet vervangen. Tijdelijke bestanden hebben unieke namen en worden bij succes/fouten opgeruimd voor zover Windows dat toelaat. Er is een procesinterne vergrendeling; dit biedt geen oplossing voor twee gelijktijdig draaiende gameprocessen die dezelfde voortgang aanpassen.

Een onbekende of ongeldige opslagversiemarkering wordt als onleesbaar behandeld: de bestaande schermen blokkeren dan opslaan. Zo'n bestand wordt ook niet als beschadigd opzijgezet. Dit voorkomt dat deze build een bestand met een nieuwere **opslagmarkering** vervangt; toekomstige wijzigingen van bijvoorbeeld het eigen carrièreformaat moeten ook in de carrièreparser worden afgehandeld.

Beschadigde JSON kan door de bestaande schermen opzijgezet worden. Een oudere `.bad` wordt nu behouden; volgende herstelbestanden krijgen `.bad.1`, `.bad.2`, enzovoort. De backup wordt niet automatisch teruggezet: de gebruiker krijgt geen stilzwijgende terugval naar oudere voortgang.

## Handmatig herstellen

Sluit de game. Bewaar eerst een kopie van de hele gebruikersmap. Om één vorige versie terug te zetten, geef je het huidige hoofdbestand een andere naam en kopieer je het bijbehorende `.bak`-bestand naar de oorspronkelijke `.json`-naam. Bewaar de `.bad`-bestanden voor foutonderzoek. Start daarna de game opnieuw.

## Overdracht aan Claude

Werkmap `C:\dev\zeus-save-updates`, branch `feat/demo-save-updates`, gebaseerd op `6d8f024`. Geen wijzigingen in Season, FamiliarityStore of andere bestanden van de actieve vertrouwdheidsagent. `SafeFile` houdt dezelfde publieke functies en leesstatussen. Nieuwe `UserSaveFile.cs` bevat de testbare opslaglaag; het bestaande Godot-testproject compileert deze rechtstreeks zonder native Godot-runtime.

De eerdere branches voor tactieken delen en annuleerbare batches zijn onafhankelijk en zitten niet in deze branch. Integreer alle drie met de actuele demobasis en bouw daarna de zip. Test in die export een bestaande gebruikersmap, een opgeslagen carrière met een nog te verwerken live uitslag, taalinstellingen en vertrouwdheid opnieuw opstarten. Die laatste volledige exportcontrole is nog open.
