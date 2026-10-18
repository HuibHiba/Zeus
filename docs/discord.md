# Discord-server voor Gaffer's Notebook

Alles wat een bot kan doen staat in `scripts/discord_server.json` (rollen, categorieën, kanalen, rechten, welkomst- en FAQ-tekst) en wordt uitgevoerd door `scripts/discord_setup.py`. Het script is idempotent: bestaande rollen en kanalen blijven, opnieuw draaien dupliceert niets. Een server aanmaken, een bot-account maken en de token aanvragen kan alleen de maker; dat is het enige handwerk.

## Wat het script instelt

| Onderdeel | Instelling |
|---|---|
| Rollen | **Maker** (beheerder, goud), **Moderator** (berichten beheren, time-out, kick, auditlog), **Tester** (groen, ziet het kanaal TESTING), **Player** (grijs) |
| Basisrechten @everyone | lezen, schrijven, reacties, bijlagen, threads, spraak; geen @everyone-mentions |
| Categorieën | INFO (alleen Maker en Moderator schrijven), GAME, TESTING (alleen Tester), NEDERLANDS, STAFF (alleen Moderator), VOICE |
| Kanalen | welcome, announcements, devlog, faq · general, feedback, bug-reports (slowmode 5 s), ideas, tactics, screenshots · tester-builds, tester-feedback · nederlands · mod-chat · voice |
| Vastgepinde berichten | welkomstbericht met regels en downloadlink in `#welcome`; FAQ in `#faq` (SmartScreen, savelocatie, F9) |
| Serverinstellingen | verificatieniveau 2 (geverifieerd e-mailadres, account ouder dan 5 min), meldingen alleen bij mentions, explicit-contentfilter voor iedereen, taal en-US, systeemkanaal `#general`, Community aan (regels = `#welcome`, updates = `#mod-chat`) |
| AutoMod | spam, mention-spam (> 8 mentions), Discord-presets voor scheldwoorden, seksuele inhoud en haatzaaien; Maker en Moderator uitgezonderd |
| Webhook | voor `#announcements`, URL opgeslagen in `%USERPROFILE%\.gaffers_discord_webhook` (geheim, nooit in git) zodat nieuws per release automatisch geplaatst kan worden |
| Icoon | `local/discord/discord-icon.png` (512 × 512), gemaakt met `scripts/make_discord_art.py` |
| Invite | permanente link zonder limiet, getoond aan het eind van het script |

De banner (`local/discord/discord-banner.png`, 960 × 540) kan pas ingesteld worden als de server boost-niveau 2 heeft (Serverinstellingen › Overzicht); het script slaat dat over.

## Stappen voor de maker (ongeveer 15 minuten)

1. Discord › **+** › *Mijn eigen* › *Voor een club of community* › naam "Gaffer's Notebook". Jij blijft eigenaar.
2. Ontwikkelaarsmodus aan (Instellingen › Geavanceerd), rechtsklik op de server › **Server-ID kopiëren**.
3. https://discord.com/developers/applications › **New Application** › naam "Gaffer's Notebook Setup" › tabblad **Bot** › **Reset Token** › kopieer de token (één keer zichtbaar). Zet **Public Bot** uit; Privileged Intents zijn niet nodig.
4. Tabblad **OAuth2 › URL Generator**: scope `bot`, permissie **Administrator**, open de gegenereerde link en voeg de bot toe aan je server. (Na de setup mag je de bot de beheerdersrechten afnemen of hem uit de server zetten; de webhook blijft werken.)
5. Bewaar de token in `%USERPROFILE%\.gaffers_discord_token` (één regel, geen aanhalingstekens). Deel de token met niemand, ook niet met Claude; zet hem niet in git of chat. Lekt hij toch: opnieuw *Reset Token*.
6. In de repo: `python scripts/make_discord_art.py` (icoon en banner), dan eerst `python scripts/discord_setup.py --guild <server-ID> --dry-run`, dan zonder `--dry-run`.
7. Kopieer de invite uit de uitvoer. Zet hem in `godot/GameInfo.cs` bij `DiscordUrl` (dan toont het Over-scherm een knop "Open Discord"), in `docs/itch/pagina.md` en op de itch-pagina, en geef hem aan Claude voor de volgende build.
8. Handmatig na afloop: geef Moderator aan wie je vertrouwt; eventueel Serverinstellingen › Onboarding voor een korte "kies je rol"-vraag (Tester of Player), en de banner zodra het kan.

## Beheer

- Nieuws plaatsen vanaf de pc: POST naar de webhook-URL uit `.gaffers_discord_webhook` met `{"content": "..."}`. Per release een devlog van ≤ 300 woorden (roadmap SJ12).
- Testers krijgen de rol **Tester** handmatig; builds worden alleen in `#tester-builds` gedeeld.
- De regels in het welkomstbericht verbieden bestanden met echte club- of spelersdata (de game gebruikt fictieve data). Dat past bij het besluit over datalicenties.
- Het spel verstuurt niets naar Discord en gebruikt geen bot; de knop in het Over-scherm opent alleen de browser (`OS.ShellOpen`).

## Niet geverifieerd

Het script is niet tegen de echte Discord-API gedraaid (geen server en geen token in deze sessie); alleen syntaxis en het JSON-bestand zijn gecontroleerd. Draai eerst met `--dry-run`. Onzeker zijn: de exacte waarden van `system_channel_flags` en AutoMod-presets, en of Community aanzetten lukt zonder extra instellingen; fouten daarbij geven een waarschuwing en stoppen het script niet.
