# Engine Support

## Included

| Engine | Status | Scope |
| --- | --- | --- |
| RPG Maker MV/MZ | Supported | RPGMVP and PNG_ conversion with key detection |
| Ren'Py | Supported | RPA 2.0, 3.0, 3.2 and 4.0 through `unrpa==2.3.0` |
| NWJS | Supported | Copies loose `www`, `package.nw` and `app.nw` files; safely unpacks ZIP-compatible `.nw` archives |
| Unity | Supported | Textures, videos, audio, OBJ meshes and direct media |
| Godot 3/4 | Supported | Unencrypted PCK versions 1, 2 and 3, including embedded PCK |
| KiriKiri | Supported | Standard unencrypted XP3 archives |
| WOLF RPG | Supported | Loose `Data` files and encrypted archives through embedded `UberWolfCli v0.6.3` |
| TyranoScript | Supported | Copies project files from the `data` folder |
| Java JAR | Supported | Safely unpacks ZIP-compatible `.jar` archives |
| Flash SWF | Experimental | Copies original SWF files and extracts embedded JPEG, PNG and GIF from `FWS` / `CWS` |
| Unreal | Experimental | Offline `.pak` extraction; ordinary, Zlib and optional AES key workflows |

## Known Limits

- Godot encrypted PCK directories and encrypted PCK files are not extracted.
- Protected game-specific XP3 variants are not extracted.
- Non-ZIP NWJS `.nw` package formats are reported and skipped.
- Flash LZMA-compressed `ZWS` files are reported and skipped.
- Unreal Oodle compression is not extracted because the single-file offline build does not download or redistribute an Oodle DLL.
- Unreal IoStore `.utoc/.ucas` containers are detected and reported, but not extracted.

## Next Priorities

1. Legacy RPG Maker XP, VX and VX Ace RGSS archives.
2. Electron `app.asar` archives.
3. GameMaker data files such as `data.win`.
4. Unity `TextAsset` and Sprite export.

[`UndertaleModTool`](https://github.com/UnderminersTeam/UndertaleModTool) is not limited to Undertale: its own documentation explicitly covers other GameMaker games. It is a useful reference for future GameMaker support, but a direct integration needs separate design work because the tool is GPL-3.0, large and focused on editing as well as unpacking.
