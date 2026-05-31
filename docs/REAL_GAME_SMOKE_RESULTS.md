# Real-game smoke results

Tested on 2026-05-31 with local copies under `D:\123123\games to test`.
Extraction outputs were written to separate `D:\123123\games test output*` folders.
The source game folders were not modified.

| Sample | Detected engine | Result |
| --- | --- | --- |
| `Flash ImpregDef` | Flash | Copied 6 SWF files and extracted 1 embedded JPEG |
| `Java Lilith's Throne (exe version)` | Java | Extracted 1588 image sources and rendered 1169 SVG files into PNG previews; produced 2757 files with no XML and no errors |
| `RPGM RedJill_SUCCESS` | RPG Maker | Auto-detected the HEX key and decrypted 5 sampled PNG files with valid PNG signatures |
| `Unity resident slut 4` | Unity | Extracted 101 files; 2829 unsupported Unity objects were skipped without errors |
| `VN Ren'Py AuraOfSin-0.1-pc` | Ren'Py | Extracted 1205 files from `archive.rpa` |
| `Ren'Py ExiliumBreeding-0.4-pc` | Ren'Py | Detected 602 loose files, 998516714 bytes; no `.rpa` archive was required |
| `Wolf RPG Chamber Game` | WOLF RPG | Copied 2626 loose `Data` files |
| `Unreal Engine Nehyr` | Unreal | Detected correctly; extraction reported that no local `oo2core*_win64.dll` was available |
| `HTML romance-rails-offline` | HTML | Detected 43 open resource files, 1165642714 bytes |
| `1QSP Zireael 1.3.1` | QSP | Detected the `.qsp` database and 1125 loose resource files, 5329752337 bytes total |
| `Rag Dark_of_the_Night_Resurrected.rag` | RAGS | Accepted standalone file input; preserved the database and recovered 10 embedded media files without errors |

The reusable audit and extraction harness lives in `tests/RealGameSmoke.cs`.
