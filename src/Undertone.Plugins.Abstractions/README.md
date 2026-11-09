# Undertone.Plugins.Abstractions

The plugin kit for [Undertone](https://github.com/Mayank-Praveen-Kumar/Undertone), a desktop music player.
Reference it with `ExcludeAssets="runtime"`: the app supplies this assembly (and Avalonia) at run time.

```xml
<PackageReference Include="Undertone.Plugins.Abstractions" Version="1.6.*" ExcludeAssets="runtime" PrivateAssets="all" />
```

Implement `IUndertonePlugin`, add a `plugin.json` manifest next to your DLL, zip the folder and
install it from Settings → Plugins → Install from file…

Full guide (manifest reference, API, permissions, safety model, packaging):
https://github.com/Mayank-Praveen-Kumar/Undertone/blob/main/docs/PLUGINS.md

## Release notes

- **1.7** (unreleased): `ILibraryReader.GetLyricLines` (the lyric lines Undertone already has for a
  track; older Undertone versions throw `NotSupportedException`).
- **1.5** (Undertone 1.6.6+): the queue (`GetQueue`, `SetUpNext`, `TracksQueued`), more `TrackInfo`
  fields, lyrics effects, `ILibraryImport`, and the size a view's window opens at
  (`IPluginViewProvider.PreferredWindowSize`, `MinWindowSize`, `WindowSizeToContent`; older Undertone
  versions ignore them).
- Earlier versions: see "Versioning and deprecation policy" in the full guide.
