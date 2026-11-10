# Undertone

Undertone is a more refined version of the original music player, with added capabilities and a simpler interface tailored to a local music library.

## Original creator and attribution

This project builds on **[Noctis](https://github.com/heartached/Noctis)**, created by **[heartached](https://github.com/heartached)**. The original creator deserves credit for the player and its foundation. This is an independent customized fork, not an official release by the original creator.

The original MIT copyright and license are preserved in [LICENSE](LICENSE).

## Added capabilities

- Single-click the player island's Lyrics button for a lyrics bubble attached to the button, with a softly blurred backdrop; double-click to use the full lyrics page.
- Cyberpunk styling and a playback-aware taskbar title.
- Automatic random library queue refills without restarting the current song.
- Lyrics lookup during song import, with sidecar files saved alongside the audio.
- EXT.to browser search and qBittorrent batch handoff.
- An in-app download panel with live progress and pause/resume controls.
- A **Show native** control for the qBittorrent desktop window.
- Simpler settings: Audio retains only Output Device; equalizer controls, the Autoplay toggle, About, Statistics, and Account & Devices are removed.
- Web remote and external-plugin loading are disabled. Integrations retain Last.fm and ListenBrainz scrobbling, and Plugins retains built-in features only.

## Versions and downloads

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

qBittorrent's local API must be configured for the download panel. Credentials are stored encrypted for the current Windows account and are never included in releases.
