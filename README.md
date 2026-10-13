# Zeus online

Tactisch voetbalspel met vrije tactieken. Ontwerp en voortgang staan in de Obsidian-vault Zeus online.

Beweging, zicht, passes, pressing/duels, replay en een wedstrijdregelbasis werken: goals, hervattingen, buitenspel bij bal spelen, twee helften, kantwissel, toegevoegde tijd en keeperreactie. Vermoeidheid, teamspelfases en blokwissels zijn in de wedstrijd gekoppeld; drie tactiektriggers (alleen `start_pressing`) worden uitgevoerd volgens v0-aannames. De aantallen zijn gekalibreerd op een symmetrisch en een ongelijk tactiekpaar (`docs/kalibratie.md`); dat is geen bewijs van voetbalrealisme.

Scheidsrechterbeoordeling gebruikt een beperkte IFAB/KNVB 2026/27-regelset voor expliciete incidenten, zonder random kaarten. Lichaamscontact/ernst en veel bijzondere gevallen ontbreken nog. Zie docs/scheidsrechter-regels.md.

Determinisme: zelfde seed, zelfde build en zelfde platform/runtime geeft dezelfde wedstrijd; identiek resultaat op een andere machine is niet gegarandeerd.

**Stand, testaantal en laatste commit: docs/hervatten.md (enige bron van waarheid).** Kalibratie: docs/kalibratie.md. Historische proef: docs/fase1b-proef.md. Claude werkt aan referentiedata, ontwerp en kalibratie; Codex aan sim-uitbreidingen.
