# Game Asset Tool

![Screenshot](screenshot.png)

[**Watch Demo Video on YouTube**](https://youtu.be/BnjRjik9fk0)

Windows tool for converting and extracting assets from **RPG Maker MV/MZ**, **Ren'Py**, **NWJS**, **Unity**, **Godot**, **KiriKiri**, **WOLF RPG**, **TyranoScript** and **Java** games. Experimental Unreal `.pak` and Flash `.swf` extraction is included.

![Version](https://img.shields.io/badge/version-1.7.0-blue)
![.NET](https://img.shields.io/badge/.NET_Framework-4.7.2-blue)
![Runtime](https://img.shields.io/badge/runtime-built--in-green)

## Highlights

- Detects the selected game engine automatically.
- Supports drag-and-drop for game folders.
- Opens with a selected folder when you drop that folder directly onto `GameAssetTool.exe`.
- Includes an `EN` / `RU` interface switch.
- Provides a **Dry Run / Scan** before extraction with archive count, candidate file count and input size.
- Shows only the extraction settings relevant to the detected engine.
- Uses one primary **Extract Assets** button and a collapsible log panel.
- Shows action tooltips, highlights folder drag-and-drop and scales for common Windows DPI settings.
- Displays the built-in runtime state while it is prepared silently in the background.
- Converts RPGMVP assets with key auto-detection and key reconstruction.
- Extracts Ren'Py RPA archives through verified `unrpa==2.3.0`.
- Extracts Unity textures, videos, audio, meshes and direct media through one built-in workflow.
- Extracts unencrypted Godot PCK archives for Godot 3 and 4.
- Extracts standard unencrypted KiriKiri XP3 archives.
- Extracts Unreal PAK archives in an experimental offline mode.
- Preserves relative paths and renames collisions with suffixes such as `image (2).png`.
- Shows the Ren'Py gallery unlocker only for relevant folders or when installed files can be removed.
- Writes an extraction summary to the results dialog and `GameAssetTool-report.txt`.

## Usage

1. Run `GameAssetTool.exe`.
2. Drop a game folder into the window or click **Browse...**.
3. Click **Dry Run / Scan** to review detected engine, archive count and input size.
4. Review the engine-specific options and click **Extract Assets**.
5. Review the results dialog and open the output folder.

Output is written inside the selected game folder:

```text
extracted/
  rpgm/
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
```

Each extractor writes `GameAssetTool-report.txt` into its own output folder.

## Ren'Py

Ren'Py extraction uses the maintained [`unrpa`](https://github.com/Lattyware/unrpa) package and supports RPA `2.0`, `3.0`, `3.2` and `4.0` archives. The pinned `unrpa==2.3.0` package is embedded into `GameAssetTool.exe`.

Archives are extracted into separate subfolders to prevent files from different RPA archives overwriting one another.

## NWJS

NWJS extraction copies loose files from `www`, `package.nw` and `app.nw` folders while preserving their relative paths. ZIP-compatible `package.nw` and `app.nw` archives are unpacked safely. Non-ZIP package formats are reported and skipped.

The bundled gallery unlocker is not used for NWJS games: gallery state is usually stored in game-specific save data.

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

The application does not download or redistribute an Oodle DLL. Oodle-compressed archives and IoStore `.utoc/.ucas` containers are reported as unsupported.

## WOLF RPG

WOLF RPG extraction copies loose `Data` files and uses the embedded MIT-licensed [`UberWolfCli v0.6.3`](https://github.com/Sinflower/UberWolf) for encrypted archives. The CLI is extracted into the temporary application session only when it is needed and removed after use.

## TyranoScript

TyranoScript extraction copies project files from the `data` folder while preserving their structure, including scenarios, images, audio and video.

## Java Games / JAR

Java `.jar` files are treated as ZIP-compatible containers and unpacked into separate folders with path traversal protection. For bundled Java games with a top-level `res` folder, `jre*` runtime folder and launcher `.exe`, loose resources are copied without duplicating the bundled JRE.

## Flash Experimental

Flash inspection copies original `.swf` files and extracts embedded JPEG, PNG and GIF images from uncompressed `FWS` and Zlib-compressed `CWS` files. LZMA-compressed `ZWS` files are reported as unsupported.

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
release/GameAssetTool-v1.7.0.exe
```

The release contains one supported executable. Users do not need any neighboring files.

## Project Layout

- `RpgmvpConverterForm.cs`: supported WinForms application.
- `scripts/`: Python sources and the portable-runtime build script.
- `tools/`: legacy and experimental utilities excluded from the release.
- `payload/`: generated embedded Python runtime archive, excluded from Git.

See [`docs/ENGINE_SUPPORT.md`](docs/ENGINE_SUPPORT.md) for limits and the next engine priorities.

## License

MIT License

## Author

SANCHEI
