# Game Asset Tool v1.6.0 UI Preview

## Main Window

The default window is compact. Only settings relevant to the detected engine are shown.

```text
+--------------------------------------------------------------------------+
| Game Asset Tool                                  Runtime: ready           |
| Drop a game folder here, scan it, then extract or unlock                 |
+--------------------------------------------------------------------------+
| Game Folder                                                              |
| [ D:\Games\Example Game                                      ] [Browse] |
|                                                                          |
| Engine: Unity                                  [ Dry Run / Scan ]        |
| 24 archive(s), 731 candidate file(s), estimated input 1.84 GB            |
|                                                                          |
| Extract Assets                                                           |
| Choose the Unity asset types to export.                                  |
| Asset type [ All v ]                                  [ Extract Assets ] |
|                                                                          |
| Gallery Unlocker for Ren'Py / NWJS                                       |
| [Soft v] [ Install Unlocker ] [ Remove Unlocker ]                        |
|                                                                          |
| [ Pause ] [ Cancel ] [ Open Output Folder ] [ Show Log ]                |
| [========================= 61% =====================]                     |
| Unity: 42 / 69                                                           |
| Archives: 42 / 69 | Size: 412.7 MB                                       |
+--------------------------------------------------------------------------+
```

## Expanded Log

The **Show Log** button expands the same fixed-width window vertically.

```text
+--------------------------------------------------------------------------+
| Log                                                         [ Clear ]    |
| Processing sharedassets0.assets                                          |
| Processing resources.assets                                              |
| ...                                                                      |
+--------------------------------------------------------------------------+
```

## Contextual Settings

- RPG Maker MV/MZ: RPGM HEX key field.
- Unity: Textures, Videos, Audio, Meshes or All filter.
- Unreal: optional Unreal AES key field and experimental-mode notice.
- Ren'Py, Godot and KiriKiri: extractor description without unnecessary inputs.
- NWJS: unlocker guidance without an asset extraction action.

## Results Dialog

Results are shown after extraction and saved as `GameAssetTool-report.txt` in the extractor output folder.
