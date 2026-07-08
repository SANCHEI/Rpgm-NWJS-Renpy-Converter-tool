# Changelog

## 2.5.0 - 2026-07-08
- Split Unity diagnostics helpers into a separate embedded Python module and keep structured JSON diagnostics alongside the HTML report.
- Added Unity dry-run object forecast so scans can show likely exportable/skipped Unity object types before extraction.
- Improved Unity Addressables bundle folder naming by choosing stronger catalog labels instead of raw hashes where possible.
- Trimmed wrapped UnityFS offset cache to active current-run entries to avoid stale diagnostic cache growth.
- Added a temporary native gallery manifest index that is deleted when the gallery window closes.
- Kept Unity diagnostics available in the HTML report while reducing reliance on loose text diagnostic files.

## 2.4.10 - 2026-07-04
- Added a Unity `Images + Video + Audio` extraction profile for media-only exports that include AudioClip samples without pulling text, meshes or animation JSON.
- Expanded Unity diagnostics with object-type export counts, skip reason summaries, wrapped UnityFS cache metadata and Addressables catalog label hints.
- Added a `What was skipped?` table to the HTML report, with copy buttons for Unity object types and skip reasons.
- Optimized the native results gallery to index previewable image/vector/video files first, show skipped non-previewable counters and expose exported `.obj` models through a lightweight `Models...` folder button.
- Cached wrapped UnityFS offsets under diagnostics/cache so repeated Addressables scans avoid rechecking the same wrapped bundles.
- Updated GitHub Actions release publishing to attach the main EXE, SHA256 and optional experimental tools ZIP while avoiding standalone experimental EXEs in the main release surface.
- Added a release checklist covering version bumps, tag workflow behavior, GitHub assets and itch.io upload hygiene.
## 2.4.9 - 2026-07-04
- Added Unity bundle fallback for Addressables files where UnityFS starts after a small wrapper/header offset.
- Recovered textures and meshes from wrapped UnityFS bundles that previously produced zero-output archive folders.
- Stopped creating empty archive output folders before an archive actually exports files.
- Added Unity diagnostics for recovered embedded UnityFS offsets.
## 2.4.8 - 2026-07-03
- Added Unity AudioClip sample extraction from Addressables bundles, including UnityPy-provided WAV samples.
- Added AnimationClip JSON export in the Everything profile so Unity animation data is no longer silently skipped.
- Improved coverage for Addressables bundles that contain audio, meshes and animation data rather than textures/videos.
## 2.4.7 - 2026-07-03
- Added Unity Addressables discovery for extensionless UnityFS/UnityWeb/UnityRaw archives inside StreamingAssets/aa and bundle folders.
- Updated Unity dry-run and bundle prompts so Addressables archives are counted before extraction.
- Added Unity diagnostics count for Addressables/signature-detected bundles.
## 2.4.6 - 2026-07-03

- Added best-effort Godot `.ctex/.stex` raw texture preview decoding for simple RGBA/RGB/L8/LA8 payloads, with clearer diagnostics for unsupported compressed texture cache files.
- Added cancellable large-preview loading in the results gallery to reduce UI stalls when switching selections or upscale-heavy previews.
- Added Dry Run detection confidence, detection notes and collection-folder warnings to help avoid scanning parent folders that contain multiple games.
- Highlighted Force engine when auto-detection confidence is low or a collection-folder warning is present.
- Added a safety prompt before extraction when the selected folder looks like a game collection rather than one exact game root.
- Hidden APK follow-up actions unless the suggested extracted target path exists.
- Added a small no-argument UnityTextLab folder-picker GUI while keeping CLI usage unchanged.
- Added a separate experimental tools ZIP for UnityTextLab releases.
- Updated the release version to 2.4.6.
## 2.4.5 - 2026-07-03

- Expanded HTML skipped diagnostics with clearer Unity filtered/unsupported-entry wording and Godot imported-cache details.
- Improved Unity Dry Run archive previews with UnityFS/UnityWeb/UnityRaw/unknown signature counts for `.assets`, `.bundle` and `.unity3d` inputs.
- Moved gallery filtering and sorting to a cancellable background task to reduce UI freezes on large result folders.
- Added Godot diagnostics for imported `.ctex/.stex` texture cache files, recovered previews and audio stream/cache containers.
- Kept Unity text extraction isolated in the separate UnityTextLab build for translation experiments instead of mixing it into the main extractor.
- Updated the release version to 2.4.5.
## 2.4.4 - 2026-07-02

- Added Unity `.unity3d` archive support for games that store main assets in `data.unity3d`; Dry Run, extraction and diagnostics now count these archives.
- Excluded service folders such as `BepInEx`, `dotnet`, `mono`, `logs` and `extracted` from Unity archive/direct-media scans to avoid collecting mod/runtime files as game assets.
- Added `ExtractionProfileResolver` to centralize Extract/Collect profile mode mapping for Unity, Java and loose-resource collection.
- Added `UnityArchiveDiscovery` to centralize Unity archive discovery, bundle-like prompts and preflight archive summaries.
- Improved Dry Run / Scan preflight with top extensions, largest inputs, route hints, Unity archive previews and excluded folder summaries.
- Improved Health diagnostics with a user-facing summary before technical details, including unsigned build and Mark-of-the-Web guidance.
- Improved gallery filtering with Huge images and Wide CG presets, Top folder grouping, search tooltip and the sprite sheet button in the toolbar.
- Renamed the loose-resource action to Collect in the UI and clarified its tooltips.
- Updated the release version to 2.4.4.

## 2.4.3 - 2026-06-27

- Added **Text only** and **Images + Text** extraction profiles for translation workflows.
- Unity extraction now exports `TextAsset` resources and direct text/localization files such as `.txt`, `.json`, `.csv`, `.xml`, `.po`, `.strings`, `.lang`, `.rpy`, `.ks` and `.bytes`.
- Loose resource collection and Ren'Py loose-resource fallback can now follow text-focused profiles instead of always collecting the same resource mix.
- Reports now count extracted text files separately from `Other`, and the results gallery can list text files with a generic file preview.
- Updated the release version to 2.4.3.

## 2.4.2 - 2026-06-26

- Fixed startup auto-path detection for Pygame/PyInstaller games so launching Game Asset Tool from inside that game folder no longer selects a parent collection folder.
- Fixed Pygame/PyInstaller `.exe` and `_internal` inputs so Dry Run and extraction use the actual game folder instead of a parent collection folder.
- File-based Godot extraction now writes to `extracted/<game name>` and only prompts to replace that game output folder.
- Fixed Godot file-input extraction so selecting an embedded-PCK `.exe` no longer scans sibling game folders in the same directory.
- Prepare the results gallery index in the background after extraction so the completion dialog opens immediately.
- Added clearer HTML skipped-output hints for Unity, Godot, Unreal and NWJS, including low-output and high-skip guidance.
- Added a GitHub Actions workflow for reproducible Windows release builds with exe and SHA-256 artifacts.
- Improved Godot imported texture recovery for `.stex` and hashed `.godot/imported` texture cache files, not only `.ctex`.
- Added Godot imported texture preview recovery for `.ctex` files that contain embedded WebP, PNG or JPEG image data.
- Added an **Engine override** dropdown for forcing extraction when auto-detection picks the wrong engine.
- Added a **Skip gallery index** option for low-memory runs and very large extraction outputs.
- Hide the follow-up extractor button when no APK follow-up exists and give it a concrete engine-specific label when it is available.
- Removed the always-visible **Clean extracted** checkbox; existing output now prompts to delete only when needed, with more robust long-path cleanup.
- Added Pygame/PyInstaller detection and recovery for XOR `0x6A` image `.dat` files used by games such as Isekai NTR Inn.
- Added a **Last Result** button that reopens the latest extraction summary, diagnostics and gallery without rerunning extraction.
- Removed the built-in **Help** menu/button from the main window to keep the header simpler.
- Added gallery tooltips for filter, sort, grouping, thumbnail size and upscale controls.
- Improved **Show in Folder** in the results gallery so Explorer opens with the selected file highlighted.
- Embedded Unity diagnostics into the HTML report as a separate **Diagnostics** tab and removed the separate Unity diagnostics TXT after report generation.
- Added per-row **Copy** buttons to the HTML report.
- Added a **Skipped summary** section to HTML reports with skipped count, likely reasons and skipped type candidates from engine diagnostics.
- Added an **HTML Report** button to the extraction results window.
- Removed obsolete old release executables, native-gallery demo build files and outdated itch upload checklists from the project tree.
- Simplified result-dialog shell opening through a shared helper.
- Removed the result-window **Skipped / Errors** and **Copy Summary** buttons; skipped/errors are now shown in the main summary and detailed in the HTML report.
- Added a lightweight **Health Check** snapshot to the HTML diagnostics.
- Reduced gallery upscale stalls by avoiding a full synchronous thumbnail list reset and throttling thumbnail redraws.
- Added persistent cached thumbnails for the results gallery so repeated filters, thumbnail sizes and upscale mode reuse generated previews.
- Added asset-class gallery filters for sprites, UI, backgrounds/CG and animation-frame candidates.
- Added a lightweight sprite-sheet preview/export tool for selected raster images in the gallery.
- Added extraction performance statistics to the HTML report, including files/sec, data/sec and process memory.
- Stopped writing `GameAssetTool-report.txt`; `GameAssetTool-report.html` is now the single persisted report file.
- Updated the release version to 2.4.2.

## 2.4.1 - 2026-06-20

- Removed the redundant Unity **Asset type** selector; Unity extraction now follows the selected **Extract** profile.
- Shortened the completion dialog to a minimal one-screen summary with compact file type and Unity diagnostic lines.
- Removed largest-file noise from the standard text and HTML reports; detailed diagnostic files remain available in the output folder.
- Fixed a Results Gallery startup crash caused by loading the initial folder before the gallery window handle exists.
- Updated the release version to 2.4.1.

## 2.4.0 - 2026-06-18

- Replaced the main results gallery with the native virtual `ListView` gallery that was tested as a separate demo build.
- Added a resizable preview panel to the main results gallery with larger image/GIF/WebP previews where Windows/.NET can decode them.
- Added gallery thumbnail size presets and an optional upscale toggle for small UI assets.
- Added gallery grouping by folder, type, kind or size without leaving virtual list mode.
- Added gallery filters for large images, small UI/sprite-like images, portrait images and landscape images.
- Read common raster image dimensions during background gallery indexing so resolution filters do not freeze the UI.
- Show image resolution in the gallery preview metadata and file tooltips when it is available.
- Added Unity extraction diagnostics written to `GameAssetTool-unity-diagnostics.txt`, including archive counts, bundle/assets split, direct media count, zero-output archives and largest archives.
- Include a compact Unity diagnostics summary in `GameAssetTool-report.txt`.
- Added a **Health** button that checks writable temp/AppData folders, embedded resources, portable Python runtime, UberWolf CLI and `resvg.exe` preflight status.
- Added extraction profiles: auto images/video, images only, images/video, everything, diagnostics only and recovery mode.
- Added richer Dry Run / Scan preflight output with profile, output path, clean-output state, runtime notes and engine-specific key/archive warnings.
- Added compact skipped-item diagnostics to extraction reports so large skip counts are easier to interpret.
- Added automatic `GameAssetTool-report.html` generation next to the existing text report.
- Added a built-in **Help** dialog covering the main workflow, profiles, supported routes, protected formats and reports.
- Refactored extraction profiles into a dedicated model so Unity, Java and loose-file modes no longer depend on raw UI indexes.
- Added a lightweight Dry Run cache for repeated scans of the same unchanged input.
- Moved Dry Run preflight formatting and local progress updates behind small reusable DTOs.
- Centralized report file enumeration in `ExtractionReportSnapshot` so report summaries reuse one captured output listing.
- Hardened release checks: version consistency, embedded resource checks and the compiled `ReleaseInspector` now run as part of the release build.
- Renamed **Collect Loose Files** to **Collect Loose Media** and clarified that it copies already-unpacked media instead of unpacking archives.
- Expanded the built-in Help window with more practical guidance for profiles, loose media, recovery, reports, gallery and health checks.
- Removed the queue buttons from the main window to keep the single-game extraction flow simpler.
- Reworked the Help window into a compact no-scroll overview with six larger sections.
- Moved results, diagnostics and help dialogs into `source/Application/Dialogs`.
- Moved the native virtual gallery implementation into `source/Application/Controls`.
- Centralized image/vector/audio/video extension handling in `MediaTypeRegistry` for reports, loose collection, APK recovery, Java resources and gallery filters.
- Improved Unity progress reporting with explicit scan, direct-media copy and archive-extraction phases.
- Strengthened Dry Run cache invalidation with a recursive file-count/size/timestamp fingerprint.
- Added release-time engine detection fixtures for GameMaker-vs-Unity priority and NWJS folders that also contain a `game` folder.
- Updated the release version to `2.4.0`.

## 2.3.1 - 2026-06-04

- Detect `.nlch` files that contain WebM video data and extract them as `.webm` for NWJS and loose-resource workflows.
- Replace the results gallery tile list with a virtualized grid so large outputs can be filtered and scrolled without creating thousands of WinForms controls.
- Add gallery tile context actions: open, show in folder and copy path.
- Remove the results gallery enlarged-preview panel and keep thumbnail loading throttled.
- Try Windows shell thumbnails for `.webm` videos and avoid repeated preview attempts for files that cannot produce thumbnails.
- Index the results gallery incrementally, reuse a lightweight `GameAssetTool-index.tsv` cache and add sorting by name, type, size or modified date.
- Cancel results gallery indexing when the gallery window is closed.
- Show live image, SVG, video and audio counters in the results gallery status bar.
- Build the results gallery index immediately after extraction so the gallery can open from cache.
- Show live extraction activity for long external operations: elapsed time, output file count, output size and the last changed output file.
- Remove animated dots from long-operation status text.
- Reduce live output polling overhead during extraction and increase Unity extraction worker scaling on multi-core CPUs.
- Replace repeated live-output folder scans with `FileSystemWatcher` updates during extraction.
- Ask for **Clean extracted** confirmation before any expensive locked-file diagnostics.
- Copy Unity direct media files in parallel.
- Save extracted Unity textures with faster PNG compression to improve export speed.
- Save decoded Unity images and streamed media through a bounded parallel writer so large archives can keep decoding while files are written.
- Add a faster default Unity **Media** mode that extracts textures and videos while skipping audio and mesh exports.
- Make Unity loose-file copying respect the selected asset type instead of copying unrelated direct media.
- Limit noisy Unity warning output so error-heavy archives do not slow the UI with thousands of log lines.
- Move Unity extractor runtime settings into a dedicated configuration class instead of keeping process environment values in the form code.
- Skip existing `extracted` folders during Unity source scanning.
- Keep the **Clean extracted** option readable while an extraction is running without making it visually clash with the rest of the interface.
- Make repeated NWJS extraction overwrite its own previous output instead of creating `file (2)` duplicates.
- Add a visible **Clean extracted** option, confirmation before deleting old results and locked-file checks before cleanup.
- Add extraction profiles for Auto, RPG Maker / NWJS, Unity, Ren'Py and Unknown / Recovery.
- Add **Collect Loose Files** modes for images plus videos, images only or all supported loose files.
- Prepare the embedded Python runtime only when an extractor that needs it is started.
- Show extracted file type counters, unknown output extensions and DAT diagnostics in the completion report.
- Show top unknown extensions in Dry Run / Scan diagnostics.
- Add DAT signature diagnostics to unknown-format reports.
- Add Android APK recovery: safely unpack ZIP-compatible APK asset entries and run signature recovery on embedded data.
- Add SRPG Studio recovery detection for `.rts`, `.dts`, `.srk` and `.srpgs` inputs.
- Add Pixel Game Maker MV recovery detection for PGMMV project/export markers and player folders.
- Expand signature recovery to carve WebM, MP3, TLG, DDS, KTX/KTX2, PVR, PKM, ASTC, CRN and QOI containers with source offsets in output names.
- Add `GameAssetTool-signatures.tsv` manifest with recovered type, offset, byte size, SHA-256 and output path.
- Deduplicate signature recovery results by SHA-256 so repeated embedded assets are not written again.
- Scan large unknown files in bounded chunks instead of skipping every file above the in-memory scan limit.
- Write ZIP/APK diagnostics for skipped, protected or unreadable archive entries.
- Add APK inner-engine hints for likely Unity, Godot, HTML/NWJS-like, GameMaker or Java payloads.
- Add APK route hints with suggested next extractor/folder and keep common inner engine containers such as Unity `.assets`, Godot `.pck` and GameMaker `data.win`.
- Expand Dry Run / Scan with top input extensions, largest input files and APK route hints.
- Run Dry Run / Scan in the background with a cancellable scan state so large folders do not freeze the UI.
- Move extraction report composition into a dedicated report builder and add largest output files plus duplicate-candidate summaries.
- Move Android APK extraction into a dedicated extractor service instead of keeping ZIP, diagnostics and signature recovery details in the form.
- Add **Run Suggested** in the results dialog for APK outputs with a detected inner Unity, Godot, GameMaker, HTML/NWJS or Java route.
- Add **Skipped / Errors** details in the results dialog, collecting report summaries and diagnostic files such as ZIP/APK skipped-entry logs.
- Write unexpected application crashes to `%LOCALAPPDATA%\GameAssetTool\GameAssetTool-last-error.log`.
- Introduce a shared local extraction cancellation context used by local copy/signature recovery routes.
- Remove obsolete report-building helper methods from the main WinForms form now that reports are composed by `ExtractionReportBuilder`.
- Add a preflight check and timeout for embedded `resvg.exe`; if Windows security blocks it, Java SVG originals are kept and PNG previews are skipped without freezing extraction.
- Make unknown-format signature recovery cancellable during large file chunk scans and ZIP entry reads.
- Include GPU texture containers in loose-resource collection and gallery image filtering.
- Show GPU/TLG image containers in the results gallery as explicit container tiles instead of attempting unavailable thumbnails.
- Preserve KiriKiri TLG6 originals with a preview note when PNG preview conversion is unavailable.
- Include TLG preview counts and preview-note counts in extraction reports.
- Write WOLF RPG diagnostics listing archive candidates and loose Data file counts.
- Extend ReleaseInspector synthetic checks for APK, SRPG Studio, Pixel Game Maker MV, signature manifests, dedupe and recovered GPU containers.

## 2.3.0 - 2026-06-03

- Show the latest three compatible BepInEx Bleeding Edge artifacts as `Latest`, `Previous` and `Fallback` choices for manual Unity downgrade testing.
- Install BepInEx transactionally through a staging folder, restore replaced managed files after failure and display the installed package inside the Unity assistant.
- Added Unity launch diagnostics for missing BepInEx logs and likely Doorstop proxy conflicts such as launchers that load `winhttp.dll` before BepInEx starts.
- Warn before installing BepInEx into Unity games that appear to use a custom `startup.exe` or `launcher.exe`.
- Keep the built-in SW_Decensor install action disabled until BepInEx has created `BepInEx/LogOutput.log` at least once.
- Added clearer Godot encrypted-PCK diagnostics for missing keys, wrong keys and detected key sources.
- Added **Copy Summary** to the extraction results dialog for easier bug reports and support messages.
- Clarified that optional BepInEx and SW_Decensor tooling cannot guarantee compatibility with every Unity game.
- Documented kumarin's permission to redistribute the embedded `SW_Decensor v0.7.4.2` package.

## 2.2.0 - 2026-06-02

- Preserve an explicitly selected or dropped directory instead of replacing it with a detected engine folder from one of its parents.
- Added experimental SPAK `.dat` container detection, streaming entry extraction, adjacent DAT collection, open-media extension inference and a manifest for protected blocks.
- Added automatic SPITE ChaCha20 decoding for media routes recovered from embedded Brotli-compressed Tauri frontend bundles, while preserving unresolved blocks under `protected/`.
- Extract uncompressed Unreal PAK entries even when other files require an unavailable Oodle decoder, with skipped entries listed in `Unreal-skipped-files.txt`.
- Added an embedded MIT-licensed `oozextract 0.5.4` fallback for offline Unreal Oodle decompression without an additional DLL download or installation.
- Added a Unity decensor assistant that detects Mono BE5, Mono BE6 and IL2CPP games, loads the latest compatible BepInEx Unity packages from the official Bleeding Edge builds page and installs the recommended package on demand.
- Embedded `SW_Decensor v0.7.4.2` with automatic BE5, BE6 or IL2CPP DLL selection and manifest-based removal that preserves unmanaged files.

## 2.1.0 - 2026-06-01

- Added unknown-format signature recovery for embedded PNG, JPEG, GIF, OGG, WAV and WebP assets while retaining a diagnostic report.
- Added GameMaker QOI and BZ2QOI texture-page conversion to PNG through an embedded script.
- Added Godot encrypted PCK directory and file extraction with a manual key field plus `keys.txt` and textual EXE-key discovery.
- Added adjacent PNG previews for supported KiriKiri TLG5 images extracted from XP3 archives.
- Added Unreal AES-key lists from the input field, `keys.txt` and textual EXE candidates, plus SHA-1 validation for extracted files.
- Added Unreal Gzip, LZ4 and Zstd decompression alongside existing Zlib and local Oodle workflows.
- Extended synthetic coverage for encrypted Godot PCK, TLG5 previews, GameMaker QOI/BZ2QOI, Windows AES-CFB128 and Unreal compression methods.

## 2.0.0 - 2026-06-01

- Split Java SVG preview rendering and Flash SWF image recovery out of the main WinForms class into dedicated modules.
- Removed duplicated standalone Unity tools, obsolete v1.3.4 source snapshots and unused itch asset-generation scripts from the working tree. Their history remains available through Git.
- Moved the supported application's C# sources into `source/` to keep the project root focused on build and release files.
- Split the main WinForms class into `Application` UI, detection and extraction-orchestration partials.
- Added an `IAssetExtractor` registry with Electron ASAR and Flash implementations.
- Added built-in Electron `app.asar` extraction and Flash FLV video recovery.
- Added archive safety limits for ZIP-compatible NWJS packages, JAR files, Electron ASAR and RGSS archives.
- Reworked the results gallery with background indexing, debounced search, scaled asynchronous thumbnails and lazy batches without a 300-file cap.
- Added an enlarged image preview panel in the gallery, kept double-click opening and removed the redundant **Open File** button.
- Applied the executable's embedded icon to the main, results and gallery windows without requiring a neighboring `.ico` file.

## 1.9.0 - 2026-05-31

- Added Java extraction modes: **Images only**, **Images + SVG previews** and **All resources**.
- Cache Java SVG preview hashes and reuse unchanged PNG previews on repeat runs without creating duplicate output files.
- Keep SVG rendering at an eight-process limit after real-game benchmarking to avoid slowing the first pass through CPU oversubscription.
- Added a searchable results gallery with thumbnails plus image, SVG, audio and video filters.
- Added built-in legacy RPG Maker XP, VX and VX Ace extraction for `.rgssad`, `.rgss2a` and `.rgss3a` archives.
- Added experimental GameMaker `data.win` recovery: preserve the original container, collect open image files and stream embedded PNG texture pages without loading the full container into memory.
- Extended synthetic checks and verified the legacy RPG Maker parser against the upstream `uuksu/RPGMakerDecrypter` test archives.

## 1.8.1 - 2026-05-31

- Changed Java extraction to keep image assets only instead of copying every loose `res` file or JAR entry.
- Preserve Java SVG originals and automatically render adjacent PNG previews.
- Embedded the official [`resvg v0.47.0`](https://github.com/linebender/resvg) Windows renderer so users do not need a browser, converter or separate installation.
- Extract `resvg.exe` only when Java SVG previews are needed and remove it with the temporary tool session.

## 1.8.0 - 2026-05-31

- Accept individual supported files through window drag-and-drop and by dropping a file directly onto `GameAssetTool.exe`.
- Added loose Ren'Py detection and collection when a game has open resources instead of `.rpa` archives.
- Added static HTML game collection with preserved folder structure.
- Added QSP database and loose-media collection without copying the bundled player.
- Added experimental RAGS `.rag` recovery: preserve the original database and carve confidently detected JPEG, PNG, GIF and OGG media.
- Added **Collect Loose Files** for copying open resources independently of archive extraction.
- Added **Export Diagnostics** for unknown game folders and files.
- Automatically discover a local Unreal `oo2core*_win64.dll` inside the game or an installed Unreal Engine while keeping Oodle handling offline.
- Extended synthetic checks and real-game smoke coverage for loose Ren'Py, HTML, QSP, RAGS, file arguments and diagnostics.

## 1.7.0 - 2026-05-30

- Accept a game-folder argument so dropping a folder onto `GameAssetTool.exe` opens the application with that path selected.
- Added WOLF RPG detection, loose `Data` copying and encrypted archive extraction through the embedded MIT-licensed `UberWolfCli v0.6.3`.
- Added TyranoScript project-data extraction.
- Added safe Java `.jar` archive extraction and loose `res` copying for bundled Java games without duplicating their JRE.
- Added experimental Flash `.swf` inspection with original-file copying and embedded JPEG, PNG and GIF extraction.
- Added an `EN` / `RU` interface switch with localized contextual hints, tooltips and results dialog.

## 1.6.1 - 2026-05-30

- Fixed Unity extraction by filtering unsupported objects before reading them and exporting meshes through UnityPy's OBJ API.
- Report Unity service objects and unavailable resources as skipped items instead of extraction errors.
- Added NWJS file extraction for loose `www`, `package.nw` and `app.nw` folders plus ZIP-compatible `.nw` archives.
- Hide the Ren'Py gallery unlocker outside relevant folders and show it again when installed files can be removed.
- Added action tooltips, drag-and-drop highlighting and DPI-aware scaling for 125% and 150% Windows display settings.

## 1.6.0 - 2026-05-30

- Reworked the main window around one `Extract Assets` action.
- Added contextual extraction settings for the detected engine.
- Kept Unity asset filters inside the contextual extraction card.
- Show RPG Maker and Unreal key input only when it is relevant.
- Added a collapsible log panel and a compact default window height.
- Added a visible silent-runtime status in the application header.
- Restricted the embedded `.rpy` gallery unlocker to Ren'Py folders and removed incorrect NWJS unlocker guidance.
- Improved disabled-button contrast so secondary actions remain readable before a folder is selected.

## 1.5.2 - 2026-05-30

- Removed recursive game-folder scanning from the first window display.
- Deferred startup folder auto-detection until after the UI is visible.
- Added fast marker-based detection for path changes and parent-folder lookup.
- Added silent background runtime preparation after selecting an engine that needs Python.
- Kept deep scanning behind explicit Dry Run / Scan and extraction actions.

## 1.5.1 - 2026-05-30

- Reduced the single-file EXE size by pruning runtime files unused by the embedded extractors.
- Replaced the heavy Unreal AES Python dependency with the built-in Windows `bcrypt.dll` API.
- Removed the upstream `pyuepak` file logger that created an unnecessary `spam.log`.
- Preserved Godot, XP3, Unreal PAK, Unreal AES, Unity, Ren'Py and offline runtime behavior.

## 1.5.0 - 2026-05-30

- Added Godot 3/4 PCK extraction for unencrypted format versions 1, 2 and 3.
- Added standard unencrypted KiriKiri XP3 extraction with compressed index and segment support.
- Added experimental Unreal `.pak` extraction through embedded `pyuepak==0.2.7`.
- Added optional Unreal AES key input through the shared key field.
- Disabled upstream Oodle DLL downloads to keep runtime extraction fully offline.
- Added clear warnings for Unreal Oodle compression, Unreal IoStore and protected archives.
- Added synthetic extractor tests for Godot PCK v1/v2/v3, XP3 and Unreal PAK.
- Added an engine support matrix and roadmap for WOLF RPG, legacy RPG Maker, Java, Flash and GameMaker.

## 1.4.0 - 2026-05-30

- Migrated the supported application to `GameAssetTool.csproj`.
- Added assembly version metadata, application icon and a single-file release build.
- Embedded Python, `unrpa==2.3.0`, `UnityPy==1.25.0` and Python extractor sources into the main executable.
- Added automatic removal of temporary `%LocalAppData%\GameAssetTool\runtime\session-*` folders.
- Unified Unity extraction inside the main application.
- Added drag-and-drop game folders, engine detection and Dry Run / Scan.
- Added collision-safe file naming, results dialog and `GameAssetTool-report.txt`.
- Added separate Install Unlocker and Remove Unlocker buttons.
- Moved legacy and experimental utilities into `tools/`.

## 1.3.4

- Preserved the previous RPG Maker and Ren'Py implementation under `tools/legacy/`.
