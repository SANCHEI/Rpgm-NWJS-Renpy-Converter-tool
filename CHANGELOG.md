# Changelog

## 1.6.0 - 2026-05-30

- Reworked the main window around one `Extract Assets` action.
- Added contextual extraction settings for the detected engine.
- Kept Unity asset filters inside the contextual extraction card.
- Show RPG Maker and Unreal key input only when it is relevant.
- Added a collapsible log panel and a compact default window height.
- Added a visible silent-runtime status in the application header.
- Restricted the embedded `.rpy` gallery unlocker to Ren'Py folders and marked NWJS as detection-only.

## 1.5.2 - 2026-05-30

- Removed recursive game-folder scanning from the first window display.
- Deferred startup folder auto-detection until after the UI is visible.
- Added fast marker-based detection for path changes and parent-folder lookup.
- Added silent background runtime preparation after selecting an engine that needs Python.
- Kept deep scanning behind explicit Dry Run / Scan and extraction actions.

## 1.5.1 - 2026-05-30

- Reduced the single-file EXE size by pruning runtime files unused by the embedded extractors.
- Replaced the heavy Unreal AES Python dependency with the built-in Windows `bcrypt.dll` API.
- Removed the upstream `pyuepak` file logger that created an unnecessary `spam.log`.
- Preserved Godot, XP3, Unreal PAK, Unreal AES, Unity, Ren'Py and offline runtime behavior.

## 1.5.0 - 2026-05-30

- Added Godot 3/4 PCK extraction for unencrypted format versions 1, 2 and 3.
- Added standard unencrypted KiriKiri XP3 extraction with compressed index and segment support.
- Added experimental Unreal `.pak` extraction through embedded `pyuepak==0.2.7`.
- Added optional Unreal AES key input through the shared key field.
- Disabled upstream Oodle DLL downloads to keep runtime extraction fully offline.
- Added clear warnings for Unreal Oodle compression, Unreal IoStore and protected archives.
- Added synthetic extractor tests for Godot PCK v1/v2/v3, XP3 and Unreal PAK.
- Added an engine support matrix and roadmap for WOLF RPG, legacy RPG Maker, Java, Flash and GameMaker.

## 1.4.0 - 2026-05-30

- Migrated the supported application to `GameAssetTool.csproj`.
- Added assembly version metadata, application icon and a single-file release build.
- Embedded Python, `unrpa==2.3.0`, `UnityPy==1.25.0` and Python extractor sources into the main executable.
- Added automatic removal of temporary `%LocalAppData%\GameAssetTool\runtime\session-*` folders.
- Unified Unity extraction inside the main application.
- Added drag-and-drop game folders, engine detection and Dry Run / Scan.
- Added collision-safe file naming, results dialog and `GameAssetTool-report.txt`.
- Added separate Install Unlocker and Remove Unlocker buttons.
- Moved legacy and experimental utilities into `tools/`.

## 1.3.4

- Preserved the previous RPG Maker and Ren'Py implementation under `tools/legacy/`.
