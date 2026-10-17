# Third-party notices (skelet)

Dit is een skelet. Het belooft niet wat er in een build zit: neem alleen de vermeldingen op voor bronnen die daadwerkelijk in die build zijn verwerkt, na controle van `README.md` in deze map.

## Godot Engine
De export bevat `GODOT_LICENSES.txt`, met de volledige engine-licentie, copyrightvermeldingen per component en bijbehorende licentieteksten. Het exportscript haalt die uit dezelfde Godot-binary als de build (`Engine.get_license_text`, `get_copyright_info`, `get_license_info`); daardoor volgt de tekst de gebruikte versie.

Bron en uitleg: https://godotengine.org/license/ en https://docs.godotengine.org/en/stable/about/complying_with_licenses.html.

## .NET-runtime
De zelfstandige export bevat de runtime-licentie en componentvermeldingen in `DOTNET_LICENSE.txt` en `DOTNET_THIRD_PARTY_NOTICES.txt`. Die worden gekopieerd uit het runtime-NuGetpakket met exact de versie die de export in zijn `runtimeconfig.json` vermeldt.

## Barlow-lettertypen

Copyright 2017 The Barlow Project Authors (https://github.com/jpt/barlow). Barlow en Barlow Condensed worden ongewijzigd meegeleverd onder de SIL Open Font License 1.1. De export bevat de volledige bijbehorende licentietekst in `FONT_OFL.txt`.

## SkillCorner Open Data
MIT-licentie, credit "SkillCorner". [Licentietekst en copyright invoegen.]

## DFL / Sportec Solutions
Bassek e.a., "An integrated dataset of spatiotemporal and event data in elite soccer", Scientific Data 12:195 (2025), CC BY 4.0. [Alleen bij gebruik in de build; wijzigingen vermelden.]

## Wyscout public dataset
Pappalardo en Massucco, "Events" (2019), https://doi.org/10.6084/m9.figshare.7770599.v1, en "Matches" (2019), https://doi.org/10.6084/m9.figshare.7770422.v1, CC BY 4.0. Beide primaire Figshare-records vermelden die licentie (gecontroleerd 2026-10-17); de lokale archieven komen overeen met de officiële MD5-waarden. Bijbehorend artikel: Pappalardo e.a., Scientific Data 6:236 (2019), https://doi.org/10.1038/s41597-019-0247-7. De export bevat `WYSCOuT_NOTICE.txt` met beide bronnen, licentielink en eigen modelbewerkingen; ruwe events en matchrecords blijven buiten het pakket. Het publicatievoorbehoud uit `Licenties en Steam` blijft gelden.

## openfootball
CC0 / public domain. Vermelding niet verplicht.

## Niet opnemen
StatsBomb en FotMob: lokaal, niet in een build.


## Lettertypen van het notitieboek

Big Shoulders Display: Copyright 2019 The Big Shoulders Project Authors (https://github.com/xotypeco/big_shoulders). De meegeleverde ExtraBold is een statische instantie op gewicht 800 van de Google Fonts-variabele font, met bijbehorende gewicht- en naammetadata aangepast. IBM Plex Sans Medium en SemiBold worden ongewijzigd betrokken uit https://github.com/IBM/plex; copyright 2018 IBM Corp. in deze bestanden (2017 in de bijbehorende licentietekst), gereserveerde fontnaam Plex. Kalam Regular: copyright 2014 Indian Type Foundry. Deze families vallen onder SIL Open Font License 1.1. Hun volledige auteurs- en licentieteksten staan naast die van Barlow in `FONT_OFL.txt` in de export.
