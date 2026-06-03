# itch.io Upload Checklist - v2.3.0

## Upload

- File: `release/GameAssetTool-v2.3.0.exe`
- Display name: `Game Asset Tool v2.3.0 - Windows x64`
- Platform: Windows
- Architecture: x64
- SHA-256: `6C64B99533A3D012E5C646E2AD4F325A51D9A75CE94220E85BC073C3D650479F`

## Page Update

- Use `itch/itch_description.md` for the release description.
- Use `itch/itch_form_template.txt` for project fields and tags.
- Upload `itch/logo-game-asset-tool.png` as the project cover image.
- Upload `itch/background-game-asset-tool.png` as the page background image.
- Upload `itch/banner-game-asset-tool.png` as the reusable page banner.
- Keep the existing demo video.

## Manual Verification

- Download the uploaded file from itch.io.
- Compare its SHA-256 with the value above.
- Launch it without neighboring files.
- Confirm that the application opens and Dry Run / Scan accepts a dropped folder or supported file.
- Drop a game folder and a `.rag` file directly onto the EXE and confirm that each path is preselected.
- Confirm that **Collect Loose Files** is available for a selected path.
- Select an unknown folder and confirm that **Recover Embedded Assets** extracts recognized media and creates a report.
- Select a Java game and confirm that all three Java extraction modes are visible.
- Complete an extraction and confirm that **Results Gallery** stays responsive while filtering, loads beyond 300 files while scrolling and shows an enlarged selected image.
- Complete an extraction and confirm that **Copy Summary** copies the results text.
- Select a Unity game, open **Unity Decensor...** and confirm that SW_Decensor stays disabled until `BepInEx/LogOutput.log` exists.
- Try an encrypted Godot PCK without a key and confirm that the report says no key candidates were found.
- Switch the interface between `EN` and `RU`.
