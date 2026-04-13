# Game Asset Tool

![Screenshot](screenshot.png)

[**Watch Demo Video on YouTube**](https://youtu.be/BnjRjik9fk0)

Fast converter for **RPGMVP**, **Unity** and **Gallery Unlocker** for **NWJS/Renpy** games.

![Version](https://img.shields.io/badge/version-1.3.0-blue)
![.NET](https://img.shields.io/badge/.NET-4.0-blue)
![Python](https://img.shields.io/badge/Python-3.x-green)

## Features

- **RPGMVP to PNG Conversion** - Fast conversion with live progress, ETA and task control (pause/cancel)
- **Unity Asset Extractor** - Extract textures, videos, audio from Unity games (.assets, .bundle files)
- **Gallery Unlocker** - Unlock galleries in Renpy/NWJS games
- **Auto-detection** - Automatically detects game type and root folder
- **Auto Python Installation** - Downloads and installs Python if not found
- **Memory Optimized** - Efficient RAM usage (~4GB) for large games

## Requirements

- Windows 7/8/10/11
- [.NET Framework 4.0](https://www.microsoft.com/en-us/download/details.aspx?id=17718) or higher
- Python 3.x with UnityPy (auto-installed)

## Download

**Latest Version:** [GameAssetTool.exe](https://github.com/SANCHEI/Rpgm-NWJS-Renpy-Converter-tool/releases/tag/v1.3.0)

## Supported Games

### RPG Maker MV/MZ
- RPGMVP encryption
- Custom HEX key support
- Auto key detection
- Parallel processing with pause/cancel

### Unity Games
- Textures (Texture2D, Sprite, Cubemap, Texture3D)
- Videos (VideoClip)
- Audio (AudioClip)
- Animation metadata
- .assets and .bundle files
- Direct media files (png, jpg, gif, mp4, etc)
- Real-time progress with ETA and elapsed time
- Bundle confirmation dialog
- Memory optimized (~4GB max)

### Renpy/NWJS Games
- Gallery unlocker (soft/hard mode)
- NWJS process detection
- RPA extraction

## Supported Engines

- **RPGM** - RPG Maker MV/MZ
- **NWJS** - Node WebKit Software (Renpy games)
- **Unity** - Unity games

## Usage

### RPGMVP Conversion
1. Select game folder (auto-detected)
2. Enter HEX key (auto-detected for known games)
3. Click Start

### Unity Extraction
1. Select Unity game folder (auto-detected)
2. Choose extraction mode: Textures / Videos / Audio / All
3. Click "Extract"
4. Confirm bundle extraction (optional)
5. Find extracted files in `game_folder/extracted/` and `game_folder/bundle_extracted/`

### Gallery Unlocker
1. Select game folder
2. Choose game type: Soft / Hard
3. Click "Unlock"

## Keyboard Shortcuts

- **Ctrl+A** in logs - Select all
- **Ctrl+C** in logs - Copy selected

## Building from Source

```batch
build_winforms.bat
```

Requires:
- .NET Framework 4.0 SDK
- CSC compiler

## Changelog

### v1.3.0
- Reduced RAM usage from 15GB to ~4GB
- Fixed bundle skip confirmation
- Added Sprite/Cubemap/Texture3D support
- Bundle files in separate folder
- Auto Python and UnityPy installation
- Unified progress format with elapsed time

### v1.2.0
- Initial Unity Asset Extractor release

## License

MIT License

## Author

SANCHEI
