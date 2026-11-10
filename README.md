# Undertone

Undertone is a more refined version of the original music player, with added capabilities and a simpler interface tailored to a local music library.

## Original creator and attribution

This project builds on **[Noctis](https://github.com/heartached/Noctis)**, created by **[heartached](https://github.com/heartached)**. The original creator deserves credit for the player and its foundation. This is an independent customized fork, not an official release by the original creator.

The original MIT copyright and license are preserved in [LICENSE](LICENSE).

## Added capabilities

- **Settings → Diagnostics**: library health, disk/cache controls and low-power mode. **Settings → Player**: named listening sessions. Undo history is also in **Settings → Diagnostics**.
- Low-power mode reduces flowing-artwork/visualizer refresh, uses one library scan worker, and defers background BPM/key analysis. Appearance preferences are preserved.
- Named sessions retain the current song, position, upcoming queue, shuffle and repeat modes. Missing songs are skipped on resume.
- Library health checks missing local files, possible duplicates, missing artwork/lyrics and incomplete tags without modifying music files.
- Undo queue additions, removals, clear/reorder operations, removed playlist songs, and deleted sessions from Undo history or Ctrl+Z outside text fields. History is limited to 20 actions per run; queue undo expires when the current song changes.

- Lower-impact library scans use two workers by default; the two-line lyrics pill avoids the per-frame word-animation loop.

- Single-click the player island's Lyrics button for a rounded, two-line lyrics pill attached to the button, with a softly blurred backdrop; click inside the pill or double-click the button to use the full lyrics page.
- Cyberpunk styling and a playback-aware taskbar title.
- Automatic random library queue refills without restarting the current song.
- Lyrics lookup during song import, with sidecar files saved alongside the audio.
- EXT.to browser search and qBittorrent batch handoff.
- An in-app download panel with live progress and pause/resume controls.
- A **Show native** control for the qBittorrent desktop window.
- Simpler settings: Audio retains only Output Device; equalizer controls, the Autoplay toggle, About, Statistics, and Account & Devices are removed.
- Web remote and external-plugin loading are disabled. Integrations retain Last.fm and ListenBrainz scrobbling, and Plugins retains built-in features only.

## Versions and downloads

For a normal Windows installation, download **Undertone-v1.6.7-Setup.exe** from the [latest release](https://github.com/Mayank-Praveen-Kumar/Undertone/releases/latest). It installs for your Windows account, adds a Start menu shortcut and an optional desktop shortcut, and supports uninstalling through Windows Settings. Your library and preferences remain in `%APPDATA%\Undertone` when you uninstall. The ZIP is also available for portable use.

The latest edition is named **Undertone**. Earlier editions retain their historical **mit-sound** names.

The current Undertone edition is built from the source in this repository. Preserved earlier compiled editions are rebranded individually and distributed as historical downloads, with the common branding and settings cleanup applied. Those historical binaries are not represented as exact source snapshots; their original individual source snapshots were not recorded.

Each edition includes an `edition.json` manifest. Release checksums identify the downloadable ZIPs. Personal libraries, account tokens, qBittorrent credentials, caches, and developer profiles are excluded.

## Build

Requires the .NET 10 SDK on Windows.

```powershell
dotnet restore src/Undertone/Undertone.csproj -p:RuntimeIdentifiers=win-x64
dotnet build src/Undertone/Undertone.csproj -c Release -r win-x64 -p:RuntimeIdentifiers=win-x64 --no-restore
```

The application, executable, assemblies and internal namespaces use **Undertone**.

To package a self-contained Windows publish folder, install [Inno Setup](https://jrsoftware.org/isdl.php) 6.5 or newer, then run:

```powershell
./scripts/build-windows-installer.ps1 -PublishDirectory ./publish/win-x64
```

The installer is written to `installer-output/`. Optional `-CompilerPath` and `-OutputDirectory` arguments let you use a local compiler or another output folder. `/NOLAUNCH` suppresses the app launch during unattended installation.

### Automated Windows builds

The [Windows installer workflow](https://github.com/Mayank-Praveen-Kumar/Undertone/actions/workflows/windows-installer.yml) builds from source on a clean GitHub-hosted Windows machine. It runs the listening-tools and Settings checks, publishes the self-contained player, and packages the installer using a checksum-verified Inno Setup compiler.

Relevant changes pushed to `main` start a build. To build manually, open **Actions → Windows installer → Run workflow**. After a successful run, download **Undertone-windows-x64-unsigned** under **Artifacts**. It contains the setup executable, SHA-256 checksum, and a manifest linking it to the source commit and build run. Artifacts are retained for 14 days; public releases remain on the Releases page.

These builds are currently unsigned. SignPath signing can be connected after the project is approved and its signing credentials and policies are configured.

qBittorrent's local API must be configured for the download panel. Credentials are stored encrypted for the current Windows account and are never included in releases.
