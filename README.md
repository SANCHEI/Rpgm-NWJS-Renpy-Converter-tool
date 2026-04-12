# RPGMVP/PNG_ to PNG Converter

A Windows desktop application for converting RPGMV encrypted image files (.rpgmvp, .png_) to standard PNG format.

![RPGMVP Converter](https://img.shields.io/badge/Platform-Windows-blue)
![.NET](https://img.shields.io/badge/.NET-4.0+-purple)
![License](https://img.shields.io/badge/License-MIT-green)

## Features

- **RPGMVP Conversion** - Convert .rpgmvp and .png_ files to PNG
- **RPA Extraction** - Extract files from Ren'Py RPA archives
- **Unlocker** - Install unlocker mods for Universal Gallery games
- **Multi-language** - English and Russian interface
- **Live Progress** - Real-time progress tracking with ETA
- **Parallel Processing** - Multi-threaded conversion for speed

## Installation

### Pre-built Executable
Download the latest release from the [Releases](https://github.com/YOUR_USERNAME/RpgmvpConverterWinForms/releases) page.

### Build from Source
```batch
cd converter_WinForms
build_winforms.bat
```
The executable will be created in the `bin` folder.

### Requirements
- Windows 7 or later
- .NET Framework 4.0 or later
- For RPA extraction: Python 3 with `unrpa` module (`pip install unrpa`)

## Usage

### RPGMVP Tab
1. Select your game folder using "Browse" or "Game Root"
2. Enter the HEX encryption key (or it will be auto-detected from System.json)
3. Click "Start" to begin conversion

### RPA Extract Tab
1. Select the game folder containing .rpa files
2. Click "Extract RPA" to extract files

### Unlocker Tab
1. Select your game folder
2. Choose Soft or Hard mode
3. Click "Unlocker" to install

## Keyboard Shortcuts
| Key | Action |
|-----|--------|
| Enter | Start conversion |

## Building

### Prerequisites
- Windows OS
- .NET Framework 4.0+ (CSC compiler)

### Files
- `Program.cs` - Application entry point
- `RpgmvpConverterForm.cs` - Main form
- `RpaExtractor.cs` - RPA file handling
- `UnlockerResources.cs` - Embedded unlocker files
- `Localization.cs` - Language strings

## Language

The application supports:
- English
- Russian (Русский)

Switch language using the dropdown in the top-right corner.

## License

MIT License - Feel free to use and modify.

## Contributing

1. Fork the repository
2. Create a feature branch
3. Commit your changes
4. Push to the branch
5. Create a Pull Request

## Support

For issues and feature requests, please use GitHub Issues.
