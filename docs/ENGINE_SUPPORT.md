# Engine Support

## Included

| Engine | Status | Scope |
| --- | --- | --- |
| RPG Maker MV/MZ | Supported | RPGMVP and PNG_ conversion with key detection |
| RPG Maker XP/VX/VX Ace | Supported | Built-in extraction for RGSSAD, RGSS2A and RGSS3A archives |
| Ren'Py | Supported | RPA 2.0, 3.0, 3.2 and 4.0 through `unrpa==2.3.0`; loose resources without RPA archives |
| NWJS | Supported | Copies loose `www`, `package.nw` and `app.nw` files; safely unpacks ZIP-compatible `.nw` archives; renames WebM-backed `.nlch` videos to `.webm` |
| Electron | Supported | Safely unpacks standard `resources/app.asar` archives |
| Unity | Supported | Textures, videos, audio, OBJ meshes and direct media; optional Mono BE5, Mono BE6 and IL2CPP decensor assistant |
| Godot 3/4 | Supported | PCK versions 1, 2, 3 and compatible 4 archives, including encrypted directory/file blocks and embedded PCK |
| KiriKiri | Supported | Standard XP3 archives plus adjacent PNG previews for supported TLG5 images |
| WOLF RPG | Supported | Loose `Data` files and encrypted archives through embedded `UberWolfCli v0.6.3` |
| TyranoScript | Supported | Copies project files from the `data` folder |
| Java games / JAR | Supported | Images-only, cached SVG-preview and all-resource modes for ZIP-compatible `.jar` archives and loose `res` folders |
| Android APK | Recovery | Safely unpacks ZIP-compatible APK assets, reports likely inner engines, suggests and can launch the next extractor route, keeps common inner engine containers and scans embedded data for media/GPU texture containers |
| HTML games | Supported | Copies open scripts, styles and media while preserving folder structure |
| QSP | Supported | Copies `.qsp` databases and loose media without duplicating the bundled player |
| Flash SWF | Experimental | Copies original SWF files and extracts embedded JPEG, PNG, GIF and FLV video from `FWS` / `CWS` |
| Unreal | Experimental | Offline `.pak` extraction; Zlib, Gzip, LZ4, Zstd, AES-key lists, built-in open-source Oodle fallback and optional local official Oodle DLL workflows |
| RAGS | Experimental | Preserves `.rag` databases and carves confidently detected JPEG, PNG, GIF and OGG media |
| GameMaker | Experimental | Preserves `data.win`, collects open images and converts embedded PNG, QOI and BZ2QOI texture pages |
| Pixel Game Maker MV | Recovery | Detects PGMMV markers, collects open resources and scans packed/encrypted data by signatures |
| SRPG Studio | Recovery | Detects `.rts`, `.dts`, `.srk` and `.srpgs`, collects open resources and scans containers by signatures |
| SPAK DAT / SPITE | Experimental | Splits SPAK `.dat`, recovers Tauri frontend media paths, decodes matching SPITE ChaCha20 payloads and preserves unresolved blocks |
| Unknown formats | Recovery | Chunk-scans files, carves embedded PNG, JPEG, GIF, OGG, WAV, WebP, WebM, MP3, TLG and GPU texture containers, writes a signatures TSV manifest and skips duplicate payloads by SHA-256 |

## Known Limits

- Godot encrypted PCK extraction still requires a discoverable or manually supplied key.
- TLG6 files are preserved and receive a preview note, but built-in PNG preview conversion still supports TLG5 only.
- GPU texture containers are recovered as original files; PNG conversion for DDS/KTX/PVR/PKM/ASTC/CRN needs a dedicated decoder backend.
- ZIP-compatible archives write diagnostics for skipped, unreadable or protected entries, but encrypted ZIP password/key recovery is not implemented.
- APK route hints point to the best next extractor/folder after unpacking and the results dialog can launch that route. They do not guarantee that every inner engine archive is fully supported.
- Non-ZIP NWJS `.nw` package formats are reported and skipped.
- Flash LZMA-compressed `ZWS` files are reported and skipped.
- Unreal Oodle compression uses a built-in MIT-licensed `oozextract` fallback. A local official `oo2core*_win64.dll` inside the selected game or an installed Unreal Engine is preferred when available. The application does not download or redistribute the official decoder.
- Unreal IoStore `.utoc/.ucas` containers are detected and reported, but not extracted.
- RAGS recovery is not a complete database parser. It preserves the original `.rag` and extracts only confidently detected embedded media.
- GameMaker recovery does not yet decode every external-texture layout.
- SPITE ChaCha20 decoding depends on runtime resource paths recovered from the embedded Tauri frontend. Unreferenced or dynamically generated paths remain `.dat` files under `protected/`.
- The Unity decensor assistant downloads a user-selected `Latest`, `Previous` or `Fallback` BepInEx Bleeding Edge artifact only after confirmation. Installation is transactional and warns before touching games with likely custom launchers. `SW_Decensor v0.7.4.2` is embedded for offline installation, but the install button is enabled only after BepInEx creates `BepInEx/LogOutput.log`. Existing user-managed BepInEx files are not overwritten or removed. Compatibility is not guaranteed for every Unity game, particularly custom launchers with Doorstop proxy conflicts.

## Next Priorities

2. Optional GPU texture decoder backend for DDS/KTX/PVR/PKM/ASTC/CRN to PNG.
3. GameMaker external texture layouts.
4. KiriKiri TLG6 preview conversion.
5. Unity `TextAsset` and Sprite export with JSON/TXT/CSV and atlas-sprite previews.

[`UndertaleModTool`](https://github.com/UnderminersTeam/UndertaleModTool) is not limited to Undertale: its own documentation explicitly covers other GameMaker games. It remains a useful format reference for extending the focused built-in recovery path without directly integrating the GPL-3.0 editor.
