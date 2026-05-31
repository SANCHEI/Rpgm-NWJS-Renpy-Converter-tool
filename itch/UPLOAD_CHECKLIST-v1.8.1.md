# itch.io Upload Checklist - v1.8.1

## Upload

- File: `release/GameAssetTool-v1.8.1.exe`
- Display name: `Game Asset Tool v1.8.1 - Windows x64`
- Platform: Windows
- Architecture: x64
- SHA-256: `92C189E6AEDB90C4616D39F20D2662D2270F1A43C2CD818E100E4AEFEBCED311`

## Page Update

- Use `itch/itch_description.md` for the release description.
- Use `itch/itch_form_template.txt` for project fields and tags.
- Upload `itch/logo-game-asset-tool.png` as the project cover image.
- Upload `itch/background-game-asset-tool.png` as the page background image.
- Keep the existing screenshot and demo video.

## Manual Verification

- Download the uploaded file from itch.io.
- Compare its SHA-256 with the value above.
- Launch it without neighboring files.
- Confirm that the application opens and Dry Run / Scan accepts a dropped folder or supported file.
- Drop a game folder and a `.rag` file directly onto the EXE and confirm that each path is preselected.
- Confirm that **Collect Loose Files** is available for a selected path.
- Select an unknown folder and confirm that **Export Diagnostics** creates a report.
- Switch the interface between `EN` and `RU`.
