# Release checklist

Use this checklist before publishing a new Game Asset Tool build.

1. Update version strings in `source/Properties/AssemblyInfo.cs`, `source/Application/RpgmvpConverterForm.Ui.cs`, `source/Reporting/OperationResult.cs`, build scripts, README and CHANGELOG.
2. Run `python -m compileall source/scripts` and `git diff --check`.
3. Build the main release with `./build_release.ps1`.
4. Build optional experimental tools with `./build_unity_text_lab.ps1`; publish them only as `GameAssetTool-tools-experimental-v*.zip`.
5. Commit and push the branch before creating a tag. A tag rerun uses the workflow file from the tagged commit, not the newest branch copy.
6. Create the GitHub release/tag from the fixed commit, then verify the Actions run uploaded the main EXE, SHA256 and optional tools ZIP.
7. Keep the public release focused on the main EXE. Do not attach standalone inspector/demo/tool EXEs unless they are inside the optional tools ZIP.
8. For itch.io, upload the main EXE, update the compact changelog, and keep optional tools clearly marked experimental.
