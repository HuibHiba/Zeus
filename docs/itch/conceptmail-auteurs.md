# Conceptmails aan databron-auteurs (NIET verstuurd)

Stand: 2026-10-17. Dit zijn concepten. Er is niets verstuurd en er staan bewust geen echte e-mailadressen in: vul `<adres>` zelf in.

## Uitleg voor de maker (NL)

**Wat willen deze mails?** Een vriendelijke check bij twee bronnen waar de licentiestatus het onzekerst is, voordat de itch-pagina Public gaat:
- **(a) Wyscout-dataset (Pappalardo en Massucco):** op Figshare staat CC BY 4.0, maar `docs/itch/licenties-check.md` punt 1 noemt een "publicatievoorbehoud" en tegenstrijdige meldingen over de voorwaarden van Wyscout zelf. De mail vraagt of gebruik van afgeleide parameters in een gratis demo akkoord is.
- **(b) SkillCorner / DFL:** de SkillCorner-README noemt geen datalicentie (alleen een verzoek om credit; de repo-LICENSE is MIT) en over de DFL-bijdrage is de CC BY 4.0 duidelijk. De mail vraagt of de vermelding klopt en akkoord is. Voor DFL (Bassek e.a.) is de licentie helder; stuur alleen mail (b) aan SkillCorner en eventueel een kopie/kort bericht aan de DFL-auteurs als je dat netjes vindt.

**Jij verstuurt ze zelf**, vanaf je eigen adres. Zoek de contactgegevens op de bronpagina's (zie `herkomst-bronnen.md`) en vul `<adres>`, `<jouw naam>` en `<link naar de itch-pagina>` in. Controleer de feitelijke zinnen (vooral "geen ruwe data in de zip") voor je verstuurt. Wacht op antwoord voordat je Public gaat; geen antwoord is geen toestemming. De mails zijn bewust kort, eerlijk en zonder overdrijving. Niets hierin is juridisch advies.

Let op: StatsBomb staat hier niet in; dat punt is een aparte beslissing (zie `statsbomb-herkomst.md`).

---

## Mail (a): auteurs van de Wyscout-dataset

**To:** `<adres Luca Pappalardo>`, `<adres Emanuele Massucco>` (zie het artikel en de Figshare-pagina)
**Subject:** Question about using derived parameters from your public soccer dataset in a free hobby game

Dear Dr. Pappalardo, dear Mr. Massucco,

I am a hobbyist developer making a free, non-commercial football tactics game ("Gaffer's Notebook", a Windows demo that I plan to release on itch.io: `<link naar de itch-pagina>`).

Some of the game's numbers are derived from your public dataset, *Soccer match event dataset* (Events: https://doi.org/10.6084/m9.figshare.7770599, Matches: https://doi.org/10.6084/m9.figshare.7770422; paper: Pappalardo et al., Scientific Data 6:236, 2019, https://doi.org/10.1038/s41597-019-0247-7). Concretely, I fitted a simple shot-probability model and a few foul, card and penalty rates from it. The game contains only these derived parameters, compiled into the program. It does not contain any raw events, player or team names, or the dataset files, and the download does not include them.

Both Figshare records state CC BY 4.0, and the download includes a notice with the attribution, the licence and a statement that I changed the data and that the work is not endorsed by you or Wyscout. I do read, however, that the dataset may come with further expectations regarding publication, and I would rather ask than assume.

Could you let me know whether this use is acceptable to you, and whether you would like the credit worded differently? I am happy to change or remove it, or to adjust the game, if you prefer.

Thank you for making the dataset public, and for your time.

Kind regards,
`<jouw naam>`
`<adres>`

---

## Mail (b): SkillCorner (en DFL-auteurs)

**To:** `<adres SkillCorner-contact>` (zie https://github.com/SkillCorner/opendata en https://skillcorner.com)
**Cc (optioneel):** `<adres corresponderende auteur Bassek e.a.>` (zie het artikel)
**Subject:** Credit and use of SkillCorner Open Data averages in a free hobby game

Hello,

I am a hobbyist developer making a free, non-commercial football tactics game ("Gaffer's Notebook", a Windows demo for itch.io: `<link naar de itch-pagina>`).

The game's default tactic line heights per phase of play are informed by averages that I computed from the SkillCorner Open Data (https://github.com/SkillCorner/opendata, A-League 2024/25), mixed with averages from the DFL/Sportec integrated dataset (Bassek, Rein, Weber, Memmert, CC BY 4.0, https://doi.org/10.1038/s41597-025-04505-y). Only these derived averages are in the game. No tracking data or other raw files are included in the download.

The README asks for credit to SkillCorner, and the README in the download (LEESMIJ.txt, English and Dutch) does so: "Default tactic line heights are informed by averages computed from SkillCorner Open Data (https://github.com/SkillCorner/opendata, credit: SkillCorner) and from Bassek et al. ... No raw data is included." The repository carries an MIT licence, but the README does not say whether this covers the data, so I would like to check with you.

Could you confirm that this use in a free, non-commercial game is fine, and whether you want the credit worded differently? I am glad to change or remove it if you prefer. (If you are not the right person, a pointer to who is would help.)

Thank you very much for releasing the data.

Kind regards,
`<jouw naam>`
`<adres>`
