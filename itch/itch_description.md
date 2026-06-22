# Game Asset Tool

**Game Asset Tool** is a portable Windows tool for extracting, collecting, previewing and diagnosing assets from visual novels and indie games.

Drop a game folder or supported file into the window, run **Dry Run / Scan**, then extract only what the detected engine supports. Output is written into an `extracted` folder inside the selected game directory, so original files are left untouched.

[Watch Demo Video](https://youtu.be/BnjRjik9fk0)

## What It Can Do

- Auto-detect the selected game engine.
- Extract archives and loose assets into organized output folders.
- Drag a folder or file onto the window, or directly onto `GameAssetTool.exe`.
- Preview results in a fast virtual gallery with search, grouping, thumbnail size presets and a resizable preview panel.
- Filter gallery output by media type, large images, small UI/sprites, portrait images and landscape images.
- View image resolution, file size, modified time and source path in the gallery preview panel.
- Copy the extraction summary for bug reports or support messages.
- Run a built-in Health Check for embedded runtime files, temp/AppData access and helper tool availability.
- Get richer Unity extraction reports with archive, bundle, direct-media and zero-output diagnostics.
- Recover embedded PNG, JPEG, GIF, OGG, WAV and WebP files from unknown formats.
- Convert Java SVG assets to PNG previews while keeping the original SVG files.
- Save WebM-backed `.nlch` videos as ordinary `.webm` files.
- Install and remove the supported Ren'Py gallery unlocker.
- Optional Unity decensor assistant for Mono BE5, Mono BE6 and IL2CPP games.
- One portable EXE. No separate Python packages or helper files are required.

## Supported Engines

**Supported / built-in workflows**

- RPG Maker MV / MZ
- RPG Maker XP / VX / VX Ace
- Ren'Py
- NWJS
- Electron
- Unity
- Godot 3 / 4
- KiriKiri
- WOLF RPG
- TyranoScript
- Java / JAR
- HTML
- QSP
- Android APK recovery

**Experimental / recovery workflows**

- Unreal Engine `.pak`
- Flash `.swf`
- RAGS `.rag`
- GameMaker `data.win`
- SRPG Studio recovery
- Pixel Game Maker MV recovery
- SPAK `.dat` / SPITE
- Unknown binary formats with recognizable embedded media

## Unity Decensor Assistant

For Unity games, the **Unity Decensor...** window can detect Mono BE5, Mono BE6 and IL2CPP setups, offer compatible BepInEx Bleeding Edge builds and install the matching embedded `SW_Decensor v0.7.4.2` DLL after BepInEx has successfully launched once.

BepInEx downloads require internet access. Extraction itself works offline.

## Notes And Limits

- Encrypted Godot PCK archives require a valid key.
- Some protected XP3, Unreal IoStore and custom archive formats are detected but not fully extracted.
- Unreal Oodle extraction uses a built-in open-source fallback; a local official `oo2core*_win64.dll` is preferred when available.
- BepInEx and SW_Decensor compatibility is not guaranteed for every Unity game, especially games with custom launchers or anti-tamper behavior.
- The Health Check can help identify blocked helper tools, but Windows SmartScreen/Defender decisions may still require manual user approval.

## Requirements

- Windows 10 version 1803 or later, or Windows 11
- 64-bit Windows
- No separate Python, package or .NET runtime installation required on supported Windows versions

The built-in runtime is prepared silently under `%LocalAppData%\GameAssetTool\runtime\` when needed and removed when the application closes.

Source code: [GitHub repository](https://github.com/SANCHEI/Rpgm-NWJS-Renpy-Converter-tool)

License: MIT
