# RPGMVP/PNG_ to PNG Converter

A Windows desktop application for converting RPGMV encrypted image files (.rpgmvp, .png_) to standard PNG format.

![Platform](https://img.shields.io/badge/Platform-Windows-blue)
![.NET](https://img.shields.io/badge/.NET-4.0+-purple)
![License](https://img.shields.io/badge/License-MIT-green)

![Screenshot](screenshot.png)

## Features

- **RPGMVP Conversion** - Convert .rpgmvp and .png_ files to PNG
- **Unlocker** - Install unlocker mods for Universal Gallery games (RPGM/NWJS)
- **Multi-language** - English and Russian interface
- **Live Progress** - Real-time progress tracking with ETA
- **Auto-detect** - Automatically finds game root and HEX key
- **Parallel Processing** - Multi-threaded conversion for speed

## Installation

### Pre-built Executable
Download the latest release from the [Releases](https://github.com/SANCHEI/Universal-tool/releases) page.

### Build from Source
```batch
cd converter_WinForms
build_winforms.bat
```
The executable will be created in the `bin` folder.

### Requirements
- Windows 7 or later
- .NET Framework 4.0 or later

## Usage

1. Select your game folder using "Browse"
2. HEX key is auto-detected from System.json
3. Click "Start" to begin conversion
4. Use "Unlock Gallery" to install gallery unlocker (RPGM/NWJS games only)

## Building

### Prerequisites
- Windows OS
- .NET Framework 4.0+ (CSC compiler)

### Project Files
```
RpgmvpConverterWinForms/
├── Program.cs              - Application entry point
├── RpgmvpConverterForm.cs - Main form
├── UnlockerResources.cs    - Embedded unlocker files
├── Localization.cs         - Language strings
├── RpaExtractor.cs         - RPA file handling
├── build_winforms.bat      - Build script
├── .github/workflows/build.yml - CI/CD
└── bin/                    - Output directory
```

## Language

The application supports:
- English (default)
- Russian (Русский)

Switch language using the RU/EN button in the top-right corner.

## License

MIT License - Feel free to use and modify.

## Support

For issues and feature requests, please use GitHub Issues.
