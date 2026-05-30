# Real-game smoke results

Tested on 2026-05-31 with local copies under `D:\123123\games to test`.
Extraction outputs were written to separate `D:\123123\games test output*` folders.
The source game folders were not modified.

| Sample | Detected engine | Result |
| --- | --- | --- |
| `Flash ImpregDef` | Flash | Copied 6 SWF files and extracted 1 embedded JPEG |
| `Java Lilith's Throne (exe version)` | Java | Copied 4093 loose `res` files without duplicating the bundled JRE |
| `RPGM RedJill_SUCCESS` | RPG Maker | Auto-detected the HEX key and decrypted 5 sampled PNG files with valid PNG signatures |
| `Unity resident slut 4` | Unity | Extracted 101 files; 2829 unsupported Unity objects were skipped without errors |
| `VN Ren'Py AuraOfSin-0.1-pc` | Ren'Py | Extracted 1205 files from `archive.rpa` |
| `Ren'Py ExiliumBreeding-0.4-pc` | Ren'Py | Detected correctly; no `.rpa` extraction was needed because the files are loose |
| `Wolf RPG Chamber Game` | WOLF RPG | Copied 2626 loose `Data` files |
| `Unreal Engine Nehyr` | Unreal | Detected correctly; extraction reported the documented offline Oodle limitation |
| `HTML romance-rails-offline` | Not supported yet | Static HTML assets are loose files |
| `QSP Zireael 1.3.1` | Not supported yet | QSP database and loose media need a dedicated collector |
| `Rag Dark_of_the_Night_Resurrected.rag` | Not supported yet | Standalone RAGS container needs file-drop support and a dedicated extractor |

The reusable audit and extraction harness lives in `tests/RealGameSmoke.cs`.
