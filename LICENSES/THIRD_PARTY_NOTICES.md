# Third-party notices (skelet)

Dit is een skelet. Het belooft niet wat er in een build zit: neem alleen de vermeldingen op voor bronnen die daadwerkelijk in die build zijn verwerkt, na controle van `README.md` in deze map.

## Godot Engine
De export bevat `GODOT_LICENSES.txt`, met de volledige engine-licentie, copyrightvermeldingen per component en bijbehorende licentieteksten. Het exportscript haalt die uit dezelfde Godot-binary als de build (`Engine.get_license_text`, `get_copyright_info`, `get_license_info`); daardoor volgt de tekst de gebruikte versie.

Bron en uitleg: https://godotengine.org/license/ en https://docs.godotengine.org/en/stable/about/complying_with_licenses.html.

## .NET-runtime
De zelfstandige export bevat de runtime-licentie en componentvermeldingen in `DOTNET_LICENSE.txt` en `DOTNET_THIRD_PARTY_NOTICES.txt`. Die worden gekopieerd uit het runtime-NuGetpakket met exact de versie die de export in zijn `runtimeconfig.json` vermeldt.

## SkillCorner Open Data
MIT-licentie, credit "SkillCorner". [Licentietekst en copyright invoegen.]

## DFL / Sportec Solutions
Bassek e.a., "An integrated dataset of spatiotemporal and event data in elite soccer", Scientific Data 12:195 (2025), CC BY 4.0. [Alleen bij gebruik in de build; wijzigingen vermelden.]

## Wyscout public dataset
Pappalardo e.a., "A public data set of spatio-temporal match events in soccer competitions", Scientific Data 6:236 (2019), CC BY 4.0. [Alleen na bevestiging van de licentie.]

## openfootball
CC0 / public domain. Vermelding niet verplicht.

## Niet opnemen
StatsBomb en FotMob: lokaal, niet in een build.
