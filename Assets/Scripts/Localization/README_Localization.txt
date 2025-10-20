Localization setup (CSV from Excel)

1) Create CSV file at Assets/Resources/localization.csv
   - First row: first cell EMPTY, then language codes in each column header: ru_RU, en_US, etc.
   - From second row: first column is the key (GameObject name that has Text/TextMeshProUGUI/TextMeshPro)
     subsequent columns are localized strings per language header.

Example:
,ru_RU,en_US
PlayButton,Играть,Play
ExitButton,Выход,Exit
HUD_Score,Счет: {0},Score: {0}

2) In scene add a GameObject with component: Game.Localization.LocalizationManager
   - It auto-loads the CSV, detects languages, applies text for the current scene.
   - It persists across scenes and reapplies on scene load.

3) Add a dropdown to UI for language switching
   - Add a TMP_Dropdown to your settings UI
   - Add component: Game.UI.LocalizationDropdown on the same object
   - It will populate options with detected languages and switch on selection.

Notes
- If Excel uses semicolons as separators, the loader auto-detects "," vs ";".
- Cells can be quoted; doubled quotes inside a quoted cell are supported.
- Keys match GameObject.name. Ensure names are unique across visible texts in a scene.
- Current language stored in PlayerPrefs key "Localization.Language".
- To update texts at runtime after CSV changes, re-enter play mode or reload the scene.

Troubleshooting
- If no languages appear, ensure the first cell of row 1 is empty and language headers are present from column 2.
- Ensure the CSV is located exactly at Assets/Resources/localization.csv (no extension change).

