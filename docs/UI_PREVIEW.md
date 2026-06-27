# Game Asset Tool v2.4.3 UI Preview

## Main Window

The default window is compact. Only settings relevant to the detected engine are shown.

```text
+--------------------------------------------------------------------------+
| Game Asset Tool                                  Runtime: ready           |
| Drop a game folder or file here, scan it, then extract or unlock         |
+--------------------------------------------------------------------------+
| Game Folder                                                              |
| [ D:\Games\Example Game                                      ] [Browse] |
|                                                                          |
| Engine: Unity                                  [ Dry Run / Scan ]        |
| 24 archive(s), 731 candidate file(s), input size 1.84 GB | Top: .assets  |
|                                                                          |
| Extract Assets                                                           |
| Choose the Unity asset types to export.                                  |
| Asset type [ All v ]                                  [ Extract Assets ] |
| [ Unity Decensor... ]                       [ Collect Loose Files ]     |
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
- RPG Maker XP/VX/VX Ace: built-in RGSS archive extraction notice.
- Unity: Textures, Videos, Audio, Meshes or All filter plus the optional **Unity Decensor...** assistant with installed-package status, `Latest` / `Previous` / `Fallback` BepInEx choices, custom-launcher warnings, launch diagnostics and SW_Decensor installation after a confirmed BepInEx launch.
- Unreal: optional Unreal AES key field and experimental-mode notice.
- Ren'Py: RPA extraction or explicit loose-resource collection without unnecessary inputs.
- Godot: optional PCK key field with automatic `keys.txt` and textual EXE-key discovery.
- KiriKiri: XP3 extraction with TLG5-to-PNG preview notice.
- NWJS: copies loose files and safely unpacks ZIP-compatible `.nw` archives without a gallery unlocker.

## Warning Panel Example

```text
! Archive warning
Some files were skipped for safety.
Reason: entry path escapes the extraction folder: ../outside.txt
Action: extracted safe files were kept. Review GameAssetTool-report.html.
```

The same panel can later be used for encrypted archives, a missing Unreal Oodle DLL or formats that require a HEX key.
- HTML and QSP: copies open project resources while preserving paths.
- RAGS: accepts a standalone `.rag` file and exposes experimental media recovery.
- Java: Images only, Images + SVG previews or All resources mode.
- GameMaker: accepts `data.win` and exposes experimental PNG, QOI and BZ2QOI texture-page recovery.
- Unknown formats: **Recover Embedded Assets** carves confidently detected media and writes a report for further analysis.

The Ren'Py gallery unlocker section is shown only for relevant Ren'Py folders or when previously installed files can be removed.

The header includes an `EN` / `RU` language switch. Dropping a folder or supported file directly onto `GameAssetTool.exe` opens the same window with that path already selected.

## Results Dialog

Results are shown after extraction and saved as `GameAssetTool-report.html` in the extractor output folder. The result window keeps only the short summary, while detailed skipped/error diagnostics live in the HTML report. APK outputs can also show a concrete follow-up extractor button to continue with the detected inner engine path. The report includes file-type counts, skipped details, health snapshot and duplicate candidates when available. The **Results Gallery** button opens a searchable thumbnail grid with image, SVG, audio and video filters. Files are indexed in the background, loaded in batches while scrolling and can be sorted without building thousands of WinForms controls.

```text
------------------------------------------------------------------+
| Extraction finished with warnings                               |
|                                                                  |
| Engine: Godot                                                    |
| Extracted files: 0                                               |
| Errors: 1                                                        |
| Output: D:\Games\Example\extracted\godot                        |
|                                                                  |
| Encrypted Godot PCK: tried 1 key candidate(s), but none passed   |
| MD5 validation. The key is likely wrong or the archive uses an    |
| unsupported encryption variant.                                  |
|                                                                  |
| [ Open Output ] [ HTML Report ] [ Results Gallery ] [ Close ]   |
+------------------------------------------------------------------+
```
