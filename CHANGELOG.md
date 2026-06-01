# Changelog

## Unreleased

- Preserve an explicitly selected or dropped directory instead of replacing it with a detected engine folder from one of its parents.
- Added experimental SPAK `.dat` container detection, streaming entry extraction, adjacent DAT collection, open-media extension inference and a manifest for protected blocks.
- Added automatic SPITE ChaCha20 decoding for media routes recovered from embedded Brotli-compressed Tauri frontend bundles, while preserving unresolved blocks under `protected/`.

## 2.1.0 - 2026-06-01

- Added unknown-format signature recovery for embedded PNG, JPEG, GIF, OGG, WAV and WebP assets while retaining a diagnostic report.
- Added GameMaker QOI and BZ2QOI texture-page conversion to PNG through an embedded script.
- Added Godot encrypted PCK directory and file extraction with a manual key field plus `keys.txt` and textual EXE-key discovery.
- Added adjacent PNG previews for supported KiriKiri TLG5 images extracted from XP3 archives.
- Added Unreal AES-key lists from the input field, `keys.txt` and textual EXE candidates, plus SHA-1 validation for extracted files.
- Added Unreal Gzip, LZ4 and Zstd decompression alongside existing Zlib and local Oodle workflows.
- Extended synthetic coverage for encrypted Godot PCK, TLG5 previews, GameMaker QOI/BZ2QOI, Windows AES-CFB128 and Unreal compression methods.

## 2.0.0 - 2026-06-01

- Split Java SVG preview rendering and Flash SWF image recovery out of the main WinForms class into dedicated modules.
- Removed duplicated standalone Unity tools, obsolete v1.3.4 source snapshots and unused itch asset-generation scripts from the working tree. Their history remains available through Git.
- Moved the supported application's C# sources into `source/` to keep the project root focused on build and release files.
- Split the main WinForms class into `Application` UI, detection and extraction-orchestration partials.
- Added an `IAssetExtractor` registry with Electron ASAR and Flash implementations.
- Added built-in Electron `app.asar` extraction and Flash FLV video recovery.
- Added archive safety limits for ZIP-compatible NWJS packages, JAR files, Electron ASAR and RGSS archives.
- Reworked the results gallery with background indexing, debounced search, scaled asynchronous thumbnails and lazy batches without a 300-file cap.
- Added an enlarged image preview panel in the gallery, kept double-click opening and removed the redundant **Open File** button.
- Applied the executable's embedded icon to the main, results and gallery windows without requiring a neighboring `.ico` file.

## 1.9.0 - 2026-05-31

- Added Java extraction modes: **Images only**, **Images + SVG previews** and **All resources**.
- Cache Java SVG preview hashes and reuse unchanged PNG previews on repeat runs without creating duplicate output files.
- Keep SVG rendering at an eight-process limit after real-game benchmarking to avoid slowing the first pass through CPU oversubscription.
- Added a searchable results gallery with thumbnails plus image, SVG, audio and video filters.
- Added built-in legacy RPG Maker XP, VX and VX Ace extraction for `.rgssad`, `.rgss2a` and `.rgss3a` archives.
- Added experimental GameMaker `data.win` recovery: preserve the original container, collect open image files and stream embedded PNG texture pages without loading the full container into memory.
- Extended synthetic checks and verified the legacy RPG Maker parser against the upstream `uuksu/RPGMakerDecrypter` test archives.

## 1.8.1 - 2026-05-31

- Changed Java extraction to keep image assets only instead of copying every loose `res` file or JAR entry.
- Preserve Java SVG originals and automatically render adjacent PNG previews.
- Embedded the official [`resvg v0.47.0`](https://github.com/linebender/resvg) Windows renderer so users do not need a browser, converter or separate installation.
- Extract `resvg.exe` only when Java SVG previews are needed and remove it with the temporary tool session.

## 1.8.0 - 2026-05-31

- Accept individual supported files through window drag-and-drop and by dropping a file directly onto `GameAssetTool.exe`.
- Added loose Ren'Py detection and collection when a game has open resources instead of `.rpa` archives.
- Added static HTML game collection with preserved folder structure.
- Added QSP database and loose-media collection without copying the bundled player.
- Added experimental RAGS `.rag` recovery: preserve the original database and carve confidently detected JPEG, PNG, GIF and OGG media.
- Added **Collect Loose Files** for copying open resources independently of archive extraction.
- Added **Export Diagnostics** for unknown game folders and files.
- Automatically discover a local Unreal `oo2core*_win64.dll` inside the game or an installed Unreal Engine while keeping Oodle handling offline.
- Extended synthetic checks and real-game smoke coverage for loose Ren'Py, HTML, QSP, RAGS, file arguments and diagnostics.

## 1.7.0 - 2026-05-30

- Accept a game-folder argument so dropping a folder onto `GameAssetTool.exe` opens the application with that path selected.
- Added WOLF RPG detection, loose `Data` copying and encrypted archive extraction through the embedded MIT-licensed `UberWolfCli v0.6.3`.
- Added TyranoScript project-data extraction.
- Added safe Java `.jar` archive extraction and loose `res` copying for bundled Java games without duplicating their JRE.
- Added experimental Flash `.swf` inspection with original-file copying and embedded JPEG, PNG and GIF extraction.
- Added an `EN` / `RU` interface switch with localized contextual hints, tooltips and results dialog.

## 1.6.1 - 2026-05-30

- Fixed Unity extraction by filtering unsupported objects before reading them and exporting meshes through UnityPy's OBJ API.
- Report Unity service objects and unavailable resources as skipped items instead of extraction errors.
- Added NWJS file extraction for loose `www`, `package.nw` and `app.nw` folders plus ZIP-compatible `.nw` archives.
- Hide the Ren'Py gallery unlocker outside relevant folders and show it again when installed files can be removed.
- Added action tooltips, drag-and-drop highlighting and DPI-aware scaling for 125% and 150% Windows display settings.

## 1.6.0 - 2026-05-30

- Reworked the main window around one `Extract Assets` action.
- Added contextual extraction settings for the detected engine.
- Kept Unity asset filters inside the contextual extraction card.
- Show RPG Maker and Unreal key input only when it is relevant.
- Added a collapsible log panel and a compact default window height.
- Added a visible silent-runtime status in the application header.
- Restricted the embedded `.rpy` gallery unlocker to Ren'Py folders and removed incorrect NWJS unlocker guidance.
- Improved disabled-button contrast so secondary actions remain readable before a folder is selected.

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
