Game Asset Tool v1.5.1 - Convert, Extract, Unlock

Drop a game folder into the app, scan it, then extract assets or install the gallery unlocker.

[Watch Demo Video](https://youtu.be/BnjRjik9fk0)

---

What's new in v1.5.1:

* Smaller single-file EXE for faster startup checks
* Unreal AES now uses the built-in Windows API instead of a large Python dependency
* Godot 3/4 PCK extraction for unencrypted archives
* Standard unencrypted KiriKiri XP3 extraction
* Experimental offline Unreal PAK extraction
* Optional Unreal AES key field
* One portable GameAssetTool.exe: no neighboring files and no runtime downloads

---

Supported engines:

- RPG Maker MV/MZ
- Ren'Py
- NWJS
- Unity
- Godot 3/4
- KiriKiri XP3
- Unreal PAK (experimental)

Requirements:

- Windows 10 version 1803 or later, or Windows 11, x64
- No separate Python, package or .NET runtime installation
- No internet connection required

Output:

- RPGM: game_folder/extracted/rpgm/
- Ren'Py: game_folder/extracted/renpy/
- Unity: game_folder/extracted/unity/
- Godot: game_folder/extracted/godot/
- KiriKiri: game_folder/extracted/kirikiri/
- Unreal: game_folder/extracted/unreal/
- Unlocker: game_folder/game/_mods/

Known limits:

- Encrypted Godot PCK and protected XP3 variants are not supported yet
- Unreal Oodle compression and IoStore .utoc/.ucas are reported but not extracted

Source Code: https://github.com/SANCHEI/Rpgm-NWJS-Renpy-Converter-tool

MIT License
