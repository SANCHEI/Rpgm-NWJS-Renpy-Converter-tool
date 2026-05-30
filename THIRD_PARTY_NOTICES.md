# Third-Party Components

Game Asset Tool embeds a private portable runtime for optional Ren'Py, Unity and Unreal extraction:

- CPython `3.12.10` embeddable distribution: https://www.python.org/
- `unrpa==2.3.0` for Ren'Py `.rpa` extraction: https://github.com/Lattyware/unrpa
- `UnityPy==1.25.0` for Unity asset extraction: https://github.com/K0lb3/UnityPy
- `pyuepak==0.2.7` for experimental Unreal `.pak` extraction: https://github.com/stas96111/pyUEpak

The upstream `pyuepak` package can acquire an Oodle DLL at runtime and normally uses the `cryptography` package for AES. Game Asset Tool replaces those integrations with an offline-only Oodle stub and the built-in Windows `bcrypt.dll` API. It does not download or redistribute the Oodle DLL. Oodle-compressed Unreal archives are reported as unsupported.

The built-in Godot PCK and standard KiriKiri XP3 extractors are project source files. The Godot implementation follows the public pack format in the official Godot source tree: https://github.com/godotengine/godot

Pinned transitive dependencies are listed in `scripts/portable-runtime-requirements.txt`. The runtime and package licenses apply to their respective components.
