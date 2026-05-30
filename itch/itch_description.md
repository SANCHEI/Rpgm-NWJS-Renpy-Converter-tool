# Game Asset Tool

**Game Asset Tool** is a portable Windows utility for extracting game assets and installing a gallery unlocker for supported visual novel and game folders.

Drop a game folder into the window, review the detected engine with **Dry Run / Scan**, then run the matching extractor. Files are written into an `extracted` folder inside the selected game directory, so the original game files remain untouched.

[Watch Demo Video](https://youtu.be/BnjRjik9fk0)

## Main Features

- Automatic engine detection after selecting or dropping a game folder
- Drag-and-drop folder support
- Dry Run / Scan preview with archive count, candidate file count and estimated input size
- Collision-safe output names such as `image (2).png`
- Extraction summary dialog and saved `GameAssetTool-report.txt`
- Separate **Install Unlocker** and **Remove Unlocker** buttons
- One portable `GameAssetTool.exe` with no neighboring files or installers
- Offline operation: no internet connection is required while using the application

## Supported Engines

### RPG Maker MV / MZ

Converts encrypted `.rpgmvp` and `.png_` image assets. The application can locate the project encryption key automatically and attempt key reconstruction when needed.

### Ren'Py

Extracts `.rpa` archives for supported RPA formats `2.0`, `3.0`, `3.2` and `4.0`.

### NWJS

Detects NWJS-style game folders and provides the gallery unlocker workflow.

### Unity

Extracts textures, videos, audio, OBJ meshes and direct media files. Unity extraction includes filters for processing only the asset types you need.

### Godot 3 / 4

Extracts standard unencrypted PCK archives, including supported PCK data embedded into an executable.

### KiriKiri

Extracts standard unencrypted `.xp3` archives, including compressed indexes and compressed file segments.

### Unreal Engine

Provides experimental offline extraction for ordinary and Zlib-compressed `.pak` archives. An optional AES key can be entered in the shared key field.

## Gallery Unlocker

The gallery unlocker is intended for supported Ren'Py and NWJS-style folders.

1. Select the game folder.
2. Choose **Soft** mode first.
3. Click **Install Unlocker**.
4. Run the game to activate it.
5. Use **Remove Unlocker** to delete the installed unlocker files.

Use **Hard** mode only when the soft mode is insufficient.

## Output Folders

- RPG Maker: `game_folder/extracted/rpgm/`
- Ren'Py: `game_folder/extracted/renpy/`
- Unity: `game_folder/extracted/unity/`
- Godot: `game_folder/extracted/godot/`
- KiriKiri: `game_folder/extracted/kirikiri/`
- Unreal: `game_folder/extracted/unreal/`
- Unlocker files: `game_folder/game/_mods/`

Each extractor writes `GameAssetTool-report.txt` into its output folder.

## Requirements

- Windows 10 version 1803 or later, or Windows 11
- 64-bit Windows
- No separate Python, package or .NET runtime installation
- No internet connection required

The built-in extraction runtime is prepared silently under `%LocalAppData%\GameAssetTool\runtime\` when it is needed and removed when the application closes.

## Known Limits

- Encrypted Godot PCK directories and encrypted Godot PCK files are not extracted.
- Protected game-specific XP3 variants are not extracted.
- Unreal Oodle compression is detected but not extracted.
- Unreal IoStore `.utoc/.ucas` containers are detected but not extracted.

Source code: [GitHub repository](https://github.com/SANCHEI/Rpgm-NWJS-Renpy-Converter-tool)

License: MIT
