# Unity Asset Extractor

Lightweight tool for extracting textures from **Unity games** (.assets, .bundle files).

![Version](https://img.shields.io/badge/version-1.0.0-blue)
![Python](https://img.shields.io/badge/Python-3.x-green)

## Features

- Extract textures (PNG) from Unity .assets files
- Extract textures from .bundle files
- Automatic skipping of localization bundles
- Real-time progress with ETA
- En/Ru localization

## Requirements

- Windows 7/8/10/11
- Python 3.x with UnityPy

```bash
pip install UnityPy
```

## Download

**Latest Release:** [UnityAssetExtractor.exe](https://github.com/SANCHEI/Rpgm-NWJS-Renpy-Converter-tool/releases/latest)

## Supported Games

Tested with:
- [Ryuugames] RY-RJ01596468 - 438 textures extracted
- BMOTV v0.7.3

Works with any Unity game that has `*_Data` folder.

## Usage

1. Select Unity game folder (should contain `*_Data` folder)
2. Choose extraction mode:
   - **Textures** - Extract PNG images
   - **Videos** - Extract video clips
   - **All** - Extract all supported assets
3. Click "Extract Unity"
4. Find extracted files in `game_folder/extracted/`

## Output Structure

```
Game/
├── *_Data/
│   ├── globalgamemanagers.assets
│   ├── sharedassets0.assets
│   └── ...
└── extracted/
    ├── globalgamemanagers/
    │   ├── texture1.png
    │   └── texture2.png
    └── sharedassets0/
        └── UI_texture.png
```

## Building from Source

```batch
build_unity.bat
```

Requires:
- .NET Framework 4.0 SDK
- CSC compiler
- Python 3.x with UnityPy

## License

MIT License

## Author

SANCHEI
