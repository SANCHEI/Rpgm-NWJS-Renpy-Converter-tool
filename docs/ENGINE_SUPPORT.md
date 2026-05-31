# Engine Support

## Included

| Engine | Status | Scope |
| --- | --- | --- |
| RPG Maker MV/MZ | Supported | RPGMVP and PNG_ conversion with key detection |
| RPG Maker XP/VX/VX Ace | Supported | Built-in extraction for RGSSAD, RGSS2A and RGSS3A archives |
| Ren'Py | Supported | RPA 2.0, 3.0, 3.2 and 4.0 through `unrpa==2.3.0`; loose resources without RPA archives |
| NWJS | Supported | Copies loose `www`, `package.nw` and `app.nw` files; safely unpacks ZIP-compatible `.nw` archives |
| Electron | Supported | Safely unpacks standard `resources/app.asar` archives |
| Unity | Supported | Textures, videos, audio, OBJ meshes and direct media |
| Godot 3/4 | Supported | Unencrypted PCK versions 1, 2 and 3, including embedded PCK |
| KiriKiri | Supported | Standard unencrypted XP3 archives |
| WOLF RPG | Supported | Loose `Data` files and encrypted archives through embedded `UberWolfCli v0.6.3` |
| TyranoScript | Supported | Copies project files from the `data` folder |
| Java games / JAR | Supported | Images-only, cached SVG-preview and all-resource modes for ZIP-compatible `.jar` archives and loose `res` folders |
| HTML games | Supported | Copies open scripts, styles and media while preserving folder structure |
| QSP | Supported | Copies `.qsp` databases and loose media without duplicating the bundled player |
| Flash SWF | Experimental | Copies original SWF files and extracts embedded JPEG, PNG, GIF and FLV video from `FWS` / `CWS` |
| Unreal | Experimental | Offline `.pak` extraction; ordinary, Zlib, optional AES key and local Oodle DLL workflows |
| RAGS | Experimental | Preserves `.rag` databases and carves confidently detected JPEG, PNG, GIF and OGG media |
| GameMaker | Experimental | Preserves `data.win`, collects open images and streams embedded PNG texture pages |

## Known Limits

- Godot encrypted PCK directories and encrypted PCK files are not extracted.
- Protected game-specific XP3 variants are not extracted.
- Non-ZIP NWJS `.nw` package formats are reported and skipped.
- Flash LZMA-compressed `ZWS` files are reported and skipped.
- Unreal Oodle compression requires a local `oo2core*_win64.dll` inside the selected game or an installed Unreal Engine. The application does not download or redistribute the decoder.
- Unreal IoStore `.utoc/.ucas` containers are detected and reported, but not extracted.
- RAGS recovery is not a complete database parser. It preserves the original `.rag` and extracts only confidently detected embedded media.
- GameMaker recovery does not yet decode newer QOI/BZ2 texture blocks or every external-texture layout.

## Next Priorities

2. GameMaker QOI/BZ2 and external texture layouts.
3. Unity `TextAsset` and Sprite export.
4. Diagnostics-guided support for additional unknown formats.

[`UndertaleModTool`](https://github.com/UnderminersTeam/UndertaleModTool) is not limited to Undertale: its own documentation explicitly covers other GameMaker games. It remains a useful format reference for extending the focused built-in recovery path without directly integrating the GPL-3.0 editor.
