# Saved versions

All recoverable local editions are preserved in [GitHub Releases](https://github.com/Mayank-Praveen-Kumar/Undertone/releases/tag/saved-versions-2026-11-09).

`undertone-current-windows-x64.zip` contains the complete current player and shared dependencies.

Each `*-version-files.zip` preserves the files that differ for that edition, its complete file inventory, and a restoration script. These archives are lossless relative to the shared base; local settings, credentials, logs and music libraries are excluded.

To restore an earlier edition:

1. Download and extract its `*-version-files.zip` into an empty folder.
2. Download `undertone-current-windows-x64.zip` into that same folder. Leave the base ZIP unextracted.
3. Run `RESTORE.ps1` in PowerShell. The complete edition appears in `restored-player`.
4. Open the player executable in `restored-player`.

The `undertone-mit-sound-*` files are the rebranded preserved editions. The `mit-sound-*` files preserve the earlier builds under their original names. The `archive-*` file preserves the additional archived build. Historical compiled editions do not have exact original source snapshots; they are not represented as reconstructed source commits.

The current source is on the repository's main branch. Original Noctis attribution and MIT license are retained.
