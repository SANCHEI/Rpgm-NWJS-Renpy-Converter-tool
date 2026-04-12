**Game Asset Tool** is a powerful utility for game modding, asset extraction, and unlocking hidden content.

[![Watch Demo](https://img.youtube.com/vi/y7z-_byQjXo/0.jpg)](https://youtu.be/y7z-_byQjXo)

## Features

- **RPGMVP / NWJS Converter** - Convert encrypted RPGMVP images to PNG with auto-detect key, live progress, ETA and pause/cancel
- **Unity Asset Extractor** - Extract textures from `.assets` and `.bundle` files (auto-installs UnityPy)
- **Renpy RPA Extraction** - Extract files from Renpy `.rpa` archives (no dependencies required)
- **Gallery Unlocker** - Unlock galleries in Renpy/NWJS games

## Supported Games

- **RPG Maker MV/MZ** - RPGMVP encryption with HEX key support
- **NWJS games** - Node WebKit applications (Renpy games)
- **Unity games** - All versions with .assets/.bundle files
- **Renpy games** - RPA archive extraction and gallery unlocker

## Requirements

- Windows 7/8/10/11
- .NET Framework 4.0
- Python 3.x (for Unity extraction - auto-installed)

## Usage

1. Run the tool
2. Select game folder (auto-detected)
3. Enter HEX key or use auto-detect
4. Click Start/Extract/Unlock

## Output

- **RPGMVP/NWJS**: Extracted PNGs in original folders
- **Unity**: Textures in `game_folder/extracted/`
- **Renpy RPA**: Extracted files in `game_folder/extracted/`
- **Unlocker**: Mod files in `game/_mods/`

---

**Demo Video**: https://youtu.be/y7z-_byQjXo

**Source Code**: https://github.com/SANCHEI/Rpgm-NWJS-Renpy-Converter-tool

MIT License
