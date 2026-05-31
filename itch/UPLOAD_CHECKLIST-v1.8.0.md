# itch.io Upload Checklist - v1.8.0

## Upload

- File: `release/GameAssetTool-v1.8.0.exe`
- Display name: `Game Asset Tool v1.8.0 - Windows x64`
- Platform: Windows
- Architecture: x64
- SHA-256: `C233E26364C4F8D31780A1D7756B37172400B22A902EE39BE4DA2F3824F873BC`

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
