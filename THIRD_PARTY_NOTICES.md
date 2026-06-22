# Third-Party Components

Game Asset Tool embeds a private portable runtime for optional Ren'Py, Unity, Unreal and SPITE extraction:

- CPython `3.12.10` embeddable distribution: https://www.python.org/
- `unrpa==2.3.0` for Ren'Py `.rpa` extraction: https://github.com/Lattyware/unrpa
- `UnityPy==1.25.0` for Unity asset extraction: https://github.com/K0lb3/UnityPy
- `pyuepak==0.2.7` for experimental Unreal `.pak` extraction: https://github.com/stas96111/pyUEpak
- `zstandard==0.25.0` for Unreal Zstd decompression: https://github.com/indygreg/python-zstandard
- `brotli==1.2.0` for embedded Tauri frontend decompression: https://github.com/google/brotli
- `pycryptodome==3.23.0` for SPITE ChaCha20 stream decoding: https://github.com/Legrandin/pycryptodome

The upstream `pyuepak` package can acquire an Oodle DLL at runtime and normally uses the `cryptography` package for AES. Game Asset Tool replaces those integrations with offline local-DLL discovery and the built-in Windows `bcrypt.dll` API. It does not download or redistribute an official Oodle DLL. For Oodle-compressed Unreal archives, it first searches for `oo2core*_win64.dll` inside the selected game and installed Unreal Engine folders, then falls back to an embedded wrapper around MIT-licensed [`oozextract==0.5.4`](https://github.com/lvlvllvlvllvlvl/oozextract). The pinned wrapper source and Cargo lockfile are stored under `source/tools/oozextract_ffi/`; its notice is stored under `third_party/oozextract/`.

The built-in Godot PCK and standard KiriKiri XP3 extractors are project source files. The Godot implementation follows the public pack format in the official Godot source tree: https://github.com/godotengine/godot

The built-in legacy RPG Maker parser follows the RGSSAD v1 and v3 archive structures used by the MIT-licensed https://github.com/uuksu/RPGMakerDecrypter project. GameMaker QOI/BZ2QOI decoding and KiriKiri TLG5 preview conversion were checked against the GPL-3.0 https://gitlab.com/gameripper/gameripper source and reimplemented as focused project code.

WOLF RPG encrypted archive extraction embeds the official `UberWolfCli v0.6.3` release artifact from https://github.com/Sinflower/UberWolf. UberWolf is distributed under the MIT License. The bundled license text is stored in `third_party/uberwolf/LICENSE.txt`. The embedded CLI SHA-256 is `FFFBE66CAF10699865010217AEABE3A3684EC9320FFE461268F1C9509FDA8917`.

Java SVG preview conversion embeds the official `resvg v0.47.0` Windows release artifact from https://github.com/linebender/resvg. Resvg is distributed under the MIT License or Apache License 2.0. The bundled license texts are stored in `third_party/resvg/`. The embedded renderer SHA-256 is `433A7C744CFF561ED64FCF73C7C04E239D7A07AE5F0AADBF1BA8471D63707402`.

The optional Unity decensor assistant does not embed BepInEx. After user confirmation it downloads a compatible Unity Mono or IL2CPP ZIP from the official https://builds.bepinex.dev/projects/bepinex_be Bleeding Edge build page. The release executable embeds the user-supplied `SW_Decensor v0.7.4.2` ZIP with SHA-256 `CC4F33FAC9E66AE22D604520F59497E946630469C55A48538164F0277DA46B0B`. Its DLL metadata identifies the copyright holder as `kumarin` and states `Copyright © 2024 kumarin All rights reserved.` Permission to redistribute was received from kumarin with the note: "source is opened. use it free."

Pinned transitive dependencies are listed in `source/scripts/portable-runtime-requirements.txt`. The runtime and package licenses apply to their respective components.
