# itch.io Upload Checklist - v1.9.0

## Upload

- File: `release/GameAssetTool-v1.9.0.exe`
- Display name: `Game Asset Tool v1.9.0 - Windows x64`
- Platform: Windows
- Architecture: x64
- SHA-256: `DA3E6AAAD722E74495D39971269F545738326D8645313014B57477B1ED763AFD`

## Page Update

- Use `itch/itch_description.md` for the release description.
- Use `itch/itch_form_template.txt` for project fields and tags.
- Upload `itch/logo-game-asset-tool.png` as the project cover image.
- Upload `itch/background-game-asset-tool.png` as the page background image.
- Keep the existing demo video.

## Manual Verification

- Download the uploaded file from itch.io.
- Compare its SHA-256 with the value above.
- Launch it without neighboring files.
- Confirm that the application opens and Dry Run / Scan accepts a dropped folder or supported file.
- Drop a game folder and a `.rag` file directly onto the EXE and confirm that each path is preselected.
- Confirm that **Collect Loose Files** is available for a selected path.
- Select an unknown folder and confirm that **Export Diagnostics** creates a report.
- Select a Java game and confirm that all three Java extraction modes are visible.
- Complete an extraction and confirm that **Results Gallery** stays responsive while filtering, loads beyond 300 files while scrolling and shows an enlarged selected image.
- Switch the interface between `EN` and `RU`.
