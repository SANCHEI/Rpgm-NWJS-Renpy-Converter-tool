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
| Godot 3/4 | Supported | PCK versions 1, 2, 3 and compatible 4 archives, including encrypted directory/file blocks and embedded PCK |
| KiriKiri | Supported | Standard XP3 archives plus adjacent PNG previews for supported TLG5 images |
| WOLF RPG | Supported | Loose `Data` files and encrypted archives through embedded `UberWolfCli v0.6.3` |
| TyranoScript | Supported | Copies project files from the `data` folder |
| Java games / JAR | Supported | Images-only, cached SVG-preview and all-resource modes for ZIP-compatible `.jar` archives and loose `res` folders |
| HTML games | Supported | Copies open scripts, styles and media while preserving folder structure |
| QSP | Supported | Copies `.qsp` databases and loose media without duplicating the bundled player |
| Flash SWF | Experimental | Copies original SWF files and extracts embedded JPEG, PNG, GIF and FLV video from `FWS` / `CWS` |
| Unreal | Experimental | Offline `.pak` extraction; Zlib, Gzip, LZ4, Zstd, AES-key lists and local Oodle DLL workflows |
| RAGS | Experimental | Preserves `.rag` databases and carves confidently detected JPEG, PNG, GIF and OGG media |
| GameMaker | Experimental | Preserves `data.win`, collects open images and converts embedded PNG, QOI and BZ2QOI texture pages |
| SPAK DAT / SPITE | Experimental | Splits SPAK `.dat`, recovers Tauri frontend media paths, decodes matching SPITE ChaCha20 payloads and preserves unresolved blocks |
| Unknown formats | Recovery | Carves embedded PNG, JPEG, GIF, OGG, WAV and WebP assets and writes diagnostics |

## Known Limits

- Godot encrypted PCK extraction still requires a discoverable or manually supplied key.
- TLG6 preview conversion and protected game-specific XP3 variants are not extracted.
- Non-ZIP NWJS `.nw` package formats are reported and skipped.
- Flash LZMA-compressed `ZWS` files are reported and skipped.
- Unreal Oodle compression requires a local `oo2core*_win64.dll` inside the selected game or an installed Unreal Engine. The application does not download or redistribute the decoder. Without it, uncompressed PAK entries are still extracted and Oodle entries are listed in `Unreal-skipped-files.txt`.
- Unreal IoStore `.utoc/.ucas` containers are detected and reported, but not extracted.
- RAGS recovery is not a complete database parser. It preserves the original `.rag` and extracts only confidently detected embedded media.
- GameMaker recovery does not yet decode every external-texture layout.
- SPITE ChaCha20 decoding depends on runtime resource paths recovered from the embedded Tauri frontend. Unreferenced or dynamically generated paths remain `.dat` files under `protected/`.

## Next Priorities

2. GameMaker external texture layouts.
3. KiriKiri TLG6 preview conversion.
4. Unity `TextAsset` and Sprite export.
5. Diagnostics-guided support for additional unknown formats.

[`UndertaleModTool`](https://github.com/UnderminersTeam/UndertaleModTool) is not limited to Undertale: its own documentation explicitly covers other GameMaker games. It remains a useful format reference for extending the focused built-in recovery path without directly integrating the GPL-3.0 editor.
