# itch.io Upload Checklist - v2.4.1

## Upload

- File: `release/GameAssetTool-v2.4.1.exe`
- Display name: `Game Asset Tool v2.4.1 - Windows x64`
- Platform: Windows
- Architecture: x64
- SHA-256: `DB11E64921A5A34C7E923DE28E0470832DA554645A7D5A911183CE5003AE8E57`

## Patch Log

- Removed the redundant Unity **Asset type** selector; Unity extraction now follows the selected **Extract** profile.
- Shortened the completion dialog to a minimal one-screen summary with compact file type and Unity diagnostic lines.
- Removed largest-file noise from the result summary and HTML report; detailed diagnostics remain available through the HTML diagnostics tab or engine-specific files where applicable.
- Fixed a Results Gallery startup crash caused by loading the initial folder before the gallery window handle exists.
- Release builds no longer run a freshly compiled `ReleaseInspector.exe`, avoiding Defender / Smart App Control blocks during packaging.
- Results Gallery controls now show tooltips, and **Show in Folder** selects the highlighted file in Explorer.
- HTML reports now include a **Diagnostics** tab with Unity diagnostics and row-level **Copy** buttons.
- Extraction results window includes an **HTML Report** button.
- HTML reports include a **Skipped summary** with count, likely reasons and skipped type candidates.
- Removed the main-window **Help** button/menu.
- Moved **Unity Decensor** nearer to the Unity extraction actions.
- Added **Last Result** for reopening the latest extraction summary/gallery without running extraction again.
- Removed obsolete old release executables, native-gallery demo build files and outdated itch upload checklists from the project tree.
- Simplified result-dialog shell opening through a shared helper.
- Removed the result-window **Skipped / Errors** and **Copy Summary** buttons; skipped/errors are now shown in the main summary and detailed in the HTML report.
- Added a lightweight **Health Check** snapshot to the HTML diagnostics.
- Reduced gallery upscale stalls by avoiding a full synchronous thumbnail list reset and throttling thumbnail redraws.
- Added persistent cached thumbnails for the results gallery so repeated filters, thumbnail sizes and upscale mode reuse generated previews.
- Added asset-class gallery filters for sprites, UI, backgrounds/CG and animation-frame candidates.
- Added a lightweight sprite-sheet preview/export tool for selected raster images in the gallery.
- Added extraction performance statistics to the HTML report, including files/sec, data/sec and process memory.
- Stopped writing `GameAssetTool-report.txt`; the HTML report is now the single persisted report file.

## Page Update

- Use `itch/itch_description.md` or `itch/itch_description.html` for the release description.
- Keep the existing logo, background, banner and demo video unless updating screenshots.
- Upload `release/GameAssetTool-v2.4.1.exe` as the current Windows build.

## Manual Verification

- Download the uploaded file from itch.io.
- Compare its SHA-256 with the value above.
- Launch it without neighboring files.
- Confirm the title shows `Game Asset Tool v2.4.1`.
- Run Unity extraction and confirm the completion dialog is short and readable.
- Confirm only `GameAssetTool-report.html` is created in the output folder; `GameAssetTool-report.txt` should not remain.
- Click **Results Gallery** from the completion dialog and confirm it opens without the startup crash.
- Confirm Unity extraction mode is selected only via the **Extract** dropdown, without a separate Asset type selector.
- Follow-up extractor button is hidden unless an APK follow-up is available, and its label names the target engine.
- Existing extracted output now asks before deletion instead of using a persistent Clean extracted checkbox; long-path cleanup is more robust.
- Godot `.ctex` imported texture caches with embedded WebP, PNG or JPEG data produce normal preview image files next to the extracted cache files.

- Use **Last Result** after closing the completion dialog and confirm the same summary/gallery opens without rerunning extraction.
- Confirm **Unity Decensor** is visible near Unity extraction actions for detected Unity games.
- Run `build_release.ps1` and confirm it reports the ReleaseInspector executable check as skipped instead of triggering Windows Defender.


- Confirm the main header no longer shows the Help button.
- Run Unity extraction and confirm `GameAssetTool-report.html` has Summary and Diagnostics tabs, while `GameAssetTool-unity-diagnostics.txt` is not left in the output folder.
- In Results Gallery, hover filter/sort/group/thumb controls and confirm tooltips appear.
- Select a gallery item, click **Show in Folder**, and confirm Explorer highlights that file.


- Confirm **Last Result** stays aligned with the action buttons after Unity hides the Ren'Py unlocker section.
- Confirm **Runtime** status and **Health** are shifted right and do not overlap the title/subtitle.


- Confirm the result dialog **HTML Report** button opens `GameAssetTool-report.html`.
- Confirm HTML diagnostics include **Skipped summary** when skipped items are present.
