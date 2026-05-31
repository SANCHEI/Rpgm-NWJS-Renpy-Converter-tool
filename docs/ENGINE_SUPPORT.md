# Engine Support

## Included

| Engine | Status | Scope |
| --- | --- | --- |
| RPG Maker MV/MZ | Supported | RPGMVP and PNG_ conversion with key detection |
| Ren'Py | Supported | RPA 2.0, 3.0, 3.2 and 4.0 through `unrpa==2.3.0`; loose resources without RPA archives |
| NWJS | Supported | Copies loose `www`, `package.nw` and `app.nw` files; safely unpacks ZIP-compatible `.nw` archives |
| Unity | Supported | Textures, videos, audio, OBJ meshes and direct media |
| Godot 3/4 | Supported | Unencrypted PCK versions 1, 2 and 3, including embedded PCK |
| KiriKiri | Supported | Standard unencrypted XP3 archives |
| WOLF RPG | Supported | Loose `Data` files and encrypted archives through embedded `UberWolfCli v0.6.3` |
| TyranoScript | Supported | Copies project files from the `data` folder |
| Java games / JAR | Supported | Safely unpacks ZIP-compatible `.jar` archives and copies loose `res` folders from bundled Java games without duplicating their JRE |
| HTML games | Supported | Copies open scripts, styles and media while preserving folder structure |
| QSP | Supported | Copies `.qsp` databases and loose media without duplicating the bundled player |
| Flash SWF | Experimental | Copies original SWF files and extracts embedded JPEG, PNG and GIF from `FWS` / `CWS` |
| Unreal | Experimental | Offline `.pak` extraction; ordinary, Zlib, optional AES key and local Oodle DLL workflows |
| RAGS | Experimental | Preserves `.rag` databases and carves confidently detected JPEG, PNG, GIF and OGG media |

## Known Limits

- Godot encrypted PCK directories and encrypted PCK files are not extracted.
- Protected game-specific XP3 variants are not extracted.
- Non-ZIP NWJS `.nw` package formats are reported and skipped.
- Flash LZMA-compressed `ZWS` files are reported and skipped.
- Unreal Oodle compression requires a local `oo2core*_win64.dll` inside the selected game or an installed Unreal Engine. The application does not download or redistribute the decoder.
- Unreal IoStore `.utoc/.ucas` containers are detected and reported, but not extracted.
- RAGS recovery is not a complete database parser. It preserves the original `.rag` and extracts only confidently detected embedded media.

## Next Priorities

1. Legacy RPG Maker XP, VX and VX Ace RGSS archives.
2. Electron `app.asar` archives.
3. GameMaker data files such as `data.win`.
4. Unity `TextAsset` and Sprite export.
5. Diagnostics-guided support for additional unknown formats.

[`UndertaleModTool`](https://github.com/UnderminersTeam/UndertaleModTool) is not limited to Undertale: its own documentation explicitly covers other GameMaker games. It is a useful reference for future GameMaker support, but a direct integration needs separate design work because the tool is GPL-3.0, large and focused on editing as well as unpacking.
