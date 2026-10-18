# itch.io Upload Checklist - v2.5.4

## Upload (manual: needs your itch.io login, cannot be automated without an API key)

- File: `release/GameAssetTool-v2.5.4.exe` (16.8 MB)
- Display name: `Game Asset Tool v2.5.4 - Windows x64`
- Platform: Windows
- Architecture: x64
- SHA-256 (this local build): `920a7ea5deb6b4c3c68c82ac5cfc2dab2a521524625cf4d628bb6f61eba60f43`
- SHA-256 (GitHub release build): `5be01cd07fa8d4fccf82d3d33793201d5cc80e2e26eb88f14be433a5da68661a`

Note: builds are not bit-reproducible, so the two SHAs above differ. Re-check the hash
immediately before uploading if the EXE was rebuilt.

## Patch Log (suggested devlog text)

### New: native Godot engine
- Godot PCK archives (v1-v4) now extract natively and offline with folder structure preserved - no portable runtime needed.
- Encrypted packs are decrypted when you supply the key or when it is found inside the game executable.
- `.ctex`/`.stex` texture resources decode to viewable images, and per-file digests are verified so corrupted entries are flagged instead of silently written.
- The portable Godot script remains as a fallback for packs the native path does not cover.

### Faster
- Plain Godot entries stream straight to disk instead of loading large packs fully into memory.
- Key discovery scans large game executables in chunks.

## Page Update

- Use `itch/itch_description.md` or `itch/itch_description.html` for the release description.
- Keep the existing logo, background, banner and demo video unless updating screenshots.
- Upload `release/GameAssetTool-v2.5.4.exe` as the current Windows build.
