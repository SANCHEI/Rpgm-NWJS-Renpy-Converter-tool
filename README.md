# Game Asset Tool

![Screenshot](screenshot.png)

[**Watch Demo Video on YouTube**](https://youtu.be/BnjRjik9fk0)

Windows tool for converting and extracting assets from **RPG Maker MV/MZ**, **RPG Maker XP/VX/VX Ace**, **Ren'Py**, **NWJS**, **Unity**, **Godot**, **KiriKiri**, **WOLF RPG**, **TyranoScript**, **Java**, **HTML** and **QSP** games. Experimental Unreal `.pak`, Flash `.swf`, RAGS `.rag` and GameMaker `data.win` recovery is included.

![Version](https://img.shields.io/badge/version-1.9.0-blue)
![.NET](https://img.shields.io/badge/.NET_Framework-4.7.2-blue)
![Runtime](https://img.shields.io/badge/runtime-built--in-green)

## Highlights

- Detects the selected game engine automatically.
- Supports drag-and-drop for game folders and individual supported files.
- Opens with a selected path when you drop a game folder or supported file directly onto `GameAssetTool.exe`.
- Includes an `EN` / `RU` interface switch.
- Provides a **Dry Run / Scan** before extraction with archive count, candidate file count and input size.
- Shows only the extraction settings relevant to the detected engine.
- Uses one primary **Extract Assets** button and a collapsible log panel.
- Shows action tooltips, highlights folder drag-and-drop and scales for common Windows DPI settings.
- Displays the built-in runtime state while it is prepared silently in the background.
- Converts RPGMVP assets with key auto-detection and key reconstruction.
- Extracts legacy RPG Maker `RGSSAD`, `RGSS2A` and `RGSS3A` archives without a neighboring helper.
- Extracts Ren'Py RPA archives through verified `unrpa==2.3.0`.
- Collects open Ren'Py resources when a game does not use RPA archives.
- Extracts Unity textures, videos, audio, meshes and direct media through one built-in workflow.
- Extracts unencrypted Godot PCK archives for Godot 3 and 4.
- Extracts standard unencrypted KiriKiri XP3 archives.
- Extracts Unreal PAK archives in an experimental offline mode.
- Collects static HTML games, QSP databases and loose resources.
- Preserves RAGS `.rag` databases and recovers confidently detected embedded media experimentally.
- Provides **Collect Loose Files** and **Export Diagnostics** fallback actions.
- Preserves relative paths and renames collisions with suffixes such as `image (2).png`.
- Opens a searchable results gallery with image previews and SVG, audio and video filters.
- Shows the Ren'Py gallery unlocker only for relevant folders or when installed files can be removed.
- Writes an extraction summary to the results dialog and `GameAssetTool-report.txt`.

## Usage

1. Run `GameAssetTool.exe`.
2. Drop a game folder or supported file into the window, or click **Browse...** for a folder.
3. Click **Dry Run / Scan** to review detected engine, archive count and input size.
4. Review the engine-specific options and click **Extract Assets**.
5. Review the results dialog and open the output folder.

Output is written inside the selected game folder:

```text
extracted/
  rpgm/
  rgss/
  renpy/
  nwjs/
  unity/
  godot/
  kirikiri/
  unreal/
  wolf/
  tyrano/
  java/
  flash/
  html/
  qsp/
  rags/
  gamemaker/
  loose/
  diagnostics/
```

Each extractor writes `GameAssetTool-report.txt` into its own output folder.

## Ren'Py

Ren'Py extraction uses the maintained [`unrpa`](https://github.com/Lattyware/unrpa) package and supports RPA `2.0`, `3.0`, `3.2` and `4.0` archives. The pinned `unrpa==2.3.0` package is embedded into `GameAssetTool.exe`.

Archives are extracted into separate subfolders to prevent files from different RPA archives overwriting one another.

When a Ren'Py game contains loose resources instead of `.rpa` archives, the application reports that mode explicitly and collects the open files into `extracted/renpy/loose/`.

## NWJS

NWJS extraction copies loose files from `www`, `package.nw` and `app.nw` folders while preserving their relative paths. ZIP-compatible `package.nw` and `app.nw` archives are unpacked safely. Non-ZIP package formats are reported and skipped.

The bundled gallery unlocker is not used for NWJS games: gallery state is usually stored in game-specific save data.

## RPG Maker XP / VX / VX Ace

Legacy RPG Maker archives with `.rgssad`, `.rgss2a` and `.rgss3a` extensions are extracted by the built-in parser. Paths are validated before writing files, and separate archives are placed into separate result folders.

## Unity

Unity extraction uses [`UnityPy==1.25.0`](https://github.com/K0lb3/UnityPy). The package, its dependencies and the Python source from [`scripts/extract_unity.py`](scripts/extract_unity.py) are embedded into the release executable during the build.

Available filters:

- Textures
- Videos
- Audio
- Meshes (`OBJ`)
- All

Bundle extraction is confirmed before processing. Direct media files are copied while preserving their relative paths.

Unity service objects and unavailable resources are reported as skipped items rather than extraction errors.

## Godot

Godot extraction supports standard unencrypted PCK format versions `1`, `2` and `3`, including PCK data embedded into an executable. Encrypted PCK directories and encrypted PCK files are reported as unsupported.

## KiriKiri

KiriKiri extraction supports standard unencrypted `.xp3` archives, compressed indexes and compressed file segments. Protected game-specific XP3 variants are reported as unsupported.

## Unreal Experimental

Unreal extraction uses [`pyuepak==0.2.7`](https://github.com/stas96111/pyUEpak) inside the built-in runtime. It supports ordinary and Zlib-compressed `.pak` archives and accepts an optional AES key in the shared key field.

The application does not download or redistribute an Oodle DLL. For Oodle-compressed archives it automatically searches for `oo2core*_win64.dll` inside the selected game and installed Unreal Engine folders. If no local decoder is available, the archive is reported and skipped. IoStore `.utoc/.ucas` containers are reported as unsupported.

## WOLF RPG

WOLF RPG extraction copies loose `Data` files and uses the embedded MIT-licensed [`UberWolfCli v0.6.3`](https://github.com/Sinflower/UberWolf) for encrypted archives. The CLI is extracted into the temporary application session only when it is needed and removed after use.

## TyranoScript

TyranoScript extraction copies project files from the `data` folder while preserving their structure, including scenarios, images, audio and video.

## Java Games / JAR

Java `.jar` files and loose `res` folders can be extracted in three modes: **Images only**, **Images + SVG previews** and **All resources**. SVG originals are preserved and optionally rendered into adjacent PNG previews through the embedded [`resvg v0.47.0`](https://github.com/linebender/resvg) helper. Preview hashes are cached so unchanged SVG files are not rendered again on later runs. The helper appears only in the temporary tool session while it is needed.

## Flash Experimental

Flash inspection copies original `.swf` files and extracts embedded JPEG, PNG and GIF images from uncompressed `FWS` and Zlib-compressed `CWS` files. LZMA-compressed `ZWS` files are reported as unsupported.

## HTML Games

Static HTML games are collected with their open scripts, styles, images, audio and video while preserving the original folder structure.

## QSP

QSP collection copies the `.qsp` database and loose media without duplicating the bundled `qsp` player folder.

## RAGS Experimental

RAGS recovery accepts a standalone `.rag` file or a folder containing one. It preserves the original database and carves confidently detected JPEG, PNG, GIF and OGG media. It is a recovery tool, not a complete RAGS database parser.

## GameMaker Experimental

GameMaker recovery accepts a folder containing `data.win` or the file itself. It preserves the original container, collects open image files and streams embedded PNG texture pages into `extracted/gamemaker/`. Newer QOI/BZ2 texture blocks and external texture layouts are reported as a current limit rather than guessed at.

## Loose Files And Diagnostics

Use **Collect Loose Files** to copy open resources independently of archive extraction. When an engine is unknown, **Export Diagnostics** writes `GameAssetTool-diagnostics.txt` with file extensions, sizes, top-level entries and signatures for further analysis.

## Gallery Unlocker

The Ren'Py unlocker is shown only when a Ren'Py folder is selected or previously installed files can be removed. It has separate buttons:

- **Install Unlocker**
- **Remove Unlocker**

Choose **Soft** first. Use **Hard** only when the soft mode is insufficient.

## Requirements

- Windows 10 version 1803 or later, or Windows 11, x64.
- No separate Python, package or .NET runtime installation is required on supported Windows versions.
- No internet connection is required while using the application.

After selecting a supported game folder, the built-in runtime is prepared silently in the background under `%LocalAppData%\GameAssetTool\runtime\`. It does not block the first window display. The session folder is removed when the application closes.

## Build

Build the main executable. The build machine needs Python 3.12 x64 and internet access the first time the pinned portable runtime is prepared:

```batch
build_winforms.bat
```

Build the single-file release:

```powershell
powershell -ExecutionPolicy Bypass -File .\build_release.ps1
```

Release output:

```text
release/GameAssetTool-v1.9.0.exe
```

The release contains one supported executable. Users do not need any neighboring files.

## Project Layout

- `source/`: C# sources for the supported WinForms application.
- `scripts/`: Python sources and the portable-runtime build script.
- `tools/`: experimental utilities excluded from the release.
- `payload/`: generated embedded Python runtime archive, excluded from Git.

See [`docs/ENGINE_SUPPORT.md`](docs/ENGINE_SUPPORT.md) for limits and the next engine priorities.

## License

MIT License

## Author

SANCHEI
