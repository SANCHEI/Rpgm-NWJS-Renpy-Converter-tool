# Game Asset Tool

**Game Asset Tool** is a portable Windows utility for extracting game assets, collecting open resources and installing a gallery unlocker for supported Ren'Py visual novels.

Drop a game folder or supported file into the window, review the detected engine with **Dry Run / Scan**, then run the matching extractor. Files are written into an `extracted` folder inside the selected game directory, so the original game files remain untouched.

[Watch Demo Video](https://youtu.be/BnjRjik9fk0)

## Main Features

- Automatic engine detection after selecting or dropping a game folder or supported file
- Drag-and-drop folder and file support
- Drop a game folder or supported file directly onto `GameAssetTool.exe` to open it with that path already selected
- `EN` / `RU` interface switch
- Dry Run / Scan preview with archive count, candidate file count and input size
- Contextual extraction options: the window shows only settings relevant to the detected engine
- **Extract Assets**, **Collect Loose Files** and unknown-format **Recover Embedded Assets** actions
- Collapsible technical log
- Action tooltips, drag-and-drop highlighting and DPI-aware scaling
- Visible runtime status while the built-in extraction tools are prepared silently
- Collision-safe output names such as `image (2).png`
- Extraction summary dialog and saved `GameAssetTool-report.txt`
- Searchable results gallery with background indexing, lazy thumbnail batches, enlarged image previews and image, SVG, audio and video filters
- Separate **Install Unlocker** and **Remove Unlocker** buttons
- One portable `GameAssetTool.exe` with no neighboring files or installers
- Offline operation: no internet connection is required while using the application

## Supported Engines

### RPG Maker MV / MZ

Converts encrypted `.rpgmvp` and `.png_` image assets. The application can locate the project encryption key automatically and attempt key reconstruction when needed.

### RPG Maker XP / VX / VX Ace

Extracts `.rgssad`, `.rgss2a` and `.rgss3a` archives through the built-in parser. No neighboring helper application is required.

### Ren'Py

Extracts `.rpa` archives for supported RPA formats `2.0`, `3.0`, `3.2` and `4.0`. If the resources are already open, they are collected without requiring an RPA archive.

### NWJS

Copies loose files from `www`, `package.nw` and `app.nw` folders while preserving paths. ZIP-compatible `.nw` archives are unpacked safely. The bundled gallery unlocker is not used for NWJS games because gallery state is usually stored in game-specific save data.

### Electron

Safely unpacks standard `resources/app.asar` archives without requiring Node.js or an external ASAR utility.

### Unity

Extracts textures, videos, audio, OBJ meshes and direct media files. Unity extraction includes filters for processing only the asset types you need.

Unity service objects and unavailable resources are reported as skipped items rather than extraction errors.

The contextual **Unity Decensor...** assistant detects Mono BE5, Mono BE6 and IL2CPP games. It fetches the three newest compatible BepInEx Bleeding Edge artifacts as `Latest`, `Previous` and `Fallback` choices, installs the selected package transactionally and installs the matching built-in `SW_Decensor v0.7.4.2` DLL. Managed removal touches only files installed by Game Asset Tool. Launch diagnostics report missing BepInEx logs and likely Doorstop proxy conflicts.

BepInEx and SW_Decensor are optional modding tools. Compatibility is not guaranteed for every Unity game, especially titles with custom launchers or anti-tamper behavior.

### Godot 3 / 4

Extracts standard and supported encrypted PCK archives, including PCK data embedded into an executable. Paste a key manually or leave the field empty to search `keys.txt` and textual key candidates inside game executables.

### KiriKiri

Extracts standard unencrypted `.xp3` archives, including compressed indexes and compressed file segments. Supported TLG5 images receive adjacent PNG previews.

### Unreal Engine

Provides experimental offline extraction for ordinary, Zlib, Gzip, LZ4, Zstd and Oodle-compressed `.pak` archives. Enter one or more AES keys or leave the field empty to search `keys.txt` and textual key candidates inside game executables. For Oodle-compressed archives the application prefers a local official `oo2core*_win64.dll` inside the game or an installed Unreal Engine, then falls back to its built-in MIT-licensed open-source decoder.

### WOLF RPG

Copies loose `Data` files and extracts encrypted WOLF archives through the embedded MIT-licensed `UberWolfCli v0.6.3`. The temporary helper is removed after use.

### TyranoScript

Copies project files from the `data` folder while preserving scenarios, images, audio, video and their original structure.

### Java Games / JAR

Provides three modes for ZIP-compatible `.jar` archives and loose `res` folders: **Images only**, **Images + SVG previews** and **All resources**. SVG previews use the built-in `resvg` helper and unchanged previews are reused from the cache on later runs.

### Flash SWF Experimental

Copies original `.swf` files and extracts embedded JPEG, PNG and GIF images plus FLV video streams from `FWS` and `CWS` files. LZMA-compressed `ZWS` files are reported as unsupported.

### HTML Games

Collects static HTML scripts, styles, images, audio and video while preserving their folder structure.

### QSP

Collects `.qsp` databases and loose media without duplicating the bundled player.

### RAGS Experimental

Accepts a standalone `.rag` file or a folder containing one. The original database is preserved and confidently detected embedded JPEG, PNG, GIF and OGG media are recovered.

### GameMaker Experimental

Accepts a folder containing `data.win` or the file itself. The original container is preserved, open images are collected and embedded PNG, QOI and BZ2QOI texture pages are recovered. QOI and BZ2QOI pages receive PNG previews.

## SPAK DAT Experimental

SPAK `.dat` containers can be split without loading the full archive into memory. For SPITE games the tool reads embedded Tauri frontend routes and decodes matching ChaCha20 media into `decoded/`. Unresolved protected blocks remain `.dat` files under `protected/`, with every result listed in `SPAK-DAT-manifest.txt`.

## Loose Files And Unknown Formats

Use **Collect Loose Files** when a game stores useful media outside its archives. For an unknown engine, **Recover Embedded Assets** extracts confidently detected PNG, JPEG, GIF, OGG, WAV and WebP files by signature and creates a diagnostic text report for further analysis.

## Gallery Unlocker

The gallery unlocker is intended for supported Ren'Py folders.

1. Select the game folder.
2. Choose **Soft** mode first.
3. Click **Install Unlocker**.
4. Run the game to activate it.
5. Use **Remove Unlocker** to delete the installed unlocker files.

Use **Hard** mode only when the soft mode is insufficient.

## Output Folders

- RPG Maker: `game_folder/extracted/rpgm/`
- RPG Maker XP/VX/VX Ace: `game_folder/extracted/rgss/`
- Ren'Py: `game_folder/extracted/renpy/`
- NWJS: `game_folder/extracted/nwjs/`
- Electron: `game_folder/extracted/electron/`
- Unity: `game_folder/extracted/unity/`
- Godot: `game_folder/extracted/godot/`
- KiriKiri: `game_folder/extracted/kirikiri/`
- Unreal: `game_folder/extracted/unreal/`
- WOLF RPG: `game_folder/extracted/wolf/`
- TyranoScript: `game_folder/extracted/tyrano/`
- Java JAR: `game_folder/extracted/java/`
- Flash SWF: `game_folder/extracted/flash/`
- HTML: `game_folder/extracted/html/`
- QSP: `game_folder/extracted/qsp/`
- RAGS: `game_folder/extracted/rags/`
- GameMaker: `game_folder/extracted/gamemaker/`
- Loose files: `game_folder/extracted/loose/`
- Unknown-format recovery: `game_folder/extracted/signature-recovery/`
- Unlocker files: `game_folder/game/_mods/`

Each extractor writes `GameAssetTool-report.txt` into its output folder.

## Requirements

- Windows 10 version 1803 or later, or Windows 11
- 64-bit Windows
- No separate Python, package or .NET runtime installation
- No internet connection required

The built-in extraction runtime is prepared silently under `%LocalAppData%\GameAssetTool\runtime\` when it is needed and removed when the application closes.

## Known Limits

- Encrypted Godot PCK extraction requires a discoverable or manually supplied key.
- TLG6 preview conversion and protected game-specific XP3 variants are not extracted.
- Unreal Oodle compression uses a built-in open-source fallback. A local official `oo2core*_win64.dll` is preferred when available. The application does not download or redistribute the official decoder.
- Unreal IoStore `.utoc/.ucas` containers are detected but not extracted.
- Flash LZMA-compressed `ZWS` files are detected but not inspected.
- RAGS recovery preserves the original database but is not a complete RAGS database parser.
- GameMaker recovery does not yet decode every external-texture layout.

Source code: [GitHub repository](https://github.com/SANCHEI/Rpgm-NWJS-Renpy-Converter-tool)

License: MIT
