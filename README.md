# RPGMVP/PNG_ -> PNG ULTRA

Fast RPGMVP to PNG converter with **Unity Asset Extractor**, **Renpy Gallery Unlocker** and full **En/Ru localization**.

![Version](https://img.shields.io/badge/version-1.0.0-blue)
![.NET](https://img.shields.io/badge/.NET-4.0-blue)
![Python](https://img.shields.io/badge/Python-3.x-green)

## Features

- **RPGMVP to PNG Conversion** - Fast conversion with live progress, ETA and task control (pause/cancel)
- **Unity Asset Extractor** - Extract textures from Unity games (.assets, .bundle files)
- **Renpy Gallery Unlocker** - Unlock galleries in Renpy/NWJS games
- **En/Ru Localization** - Switch between English and Russian
- **Auto-detection** - Automatically detects game type and root folder

## Requirements

- Windows 7/8/10/11
- [.NET Framework 4.0](https://www.microsoft.com/en-us/download/details.aspx?id=17718) or higher
- Python 3.x with UnityPy (for Unity extraction)

### Unity Extraction

```bash
pip install UnityPy
```

## Download

**Latest Release:** [RpgmvpConverterWinForms.exe](https://github.com/SANCHEI/Rpgm-NWJS-Renpy-Converter-tool/releases/latest)

For **Unity games only**, use the lighter version without RPGMVP converter.

## Supported Games

### RPG Maker MV/MZ
- RPGMVP encryption
- Custom HEX key support

### Unity Games
- Textures from .assets files
- Textures from .bundle files
- Automatic skipping of localization bundles

### Renpy/NWJS Games
- Gallery unlocker
- NWJS process detection

## Usage

### RPGMVP Conversion
1. Select game folder (auto-detected)
2. Enter HEX key (auto-detected for known games)
3. Click Start

### Unity Extraction
1. Select Unity game folder
2. Choose extraction mode: Textures / Videos / All
3. Click "Extract Unity"

### Gallery Unlocker
1. Select game folder
2. Choose game type: Renpy / NWJS
3. Click "Unlock"

## Keyboard Shortcuts

- **Ctrl+A** in logs - Select all
- **Ctrl+C** in logs - Copy selected

## Screenshots

```
┌─────────────────────────────────────────────────────┐
│  RPGMVP/PNG_ -> PNG ULTRA                           │
│  Fast conversion with live progress                  │
├─────────────────────────────────────────────────────┤
│  Game Root: [D:\Games\MyGame            ] [G] [...] │
│  HEX key:   [xxxxxxxxxxxxxxxx             ]         │
├─────────────────────────────────────────────────────┤
│  [▶ Start]  [⏸ Pause]  [✕ Cancel]                   │
├─────────────────────────────────────────────────────┤
│  Progress: ████████████░░░░░░░░ 67%                │
│  Processed: 156 / 233 | Speed: 12.5 f/s | ETA: 00:06│
├─────────────────────────────────────────────────────┤
│  Log:                                               │
│  > Processing: img/faceset1.png                     │
│  > Processing: img/faceset2.png                     │
│  > Done! 233 files in 18.6s                         │
└─────────────────────────────────────────────────────┘
```

## Building from Source

```batch
build_winforms.bat
```

Requires:
- .NET Framework 4.0 SDK
- CSC compiler

## License

MIT License

## Author

SANCHEI
