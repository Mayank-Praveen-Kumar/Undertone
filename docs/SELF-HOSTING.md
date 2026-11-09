# Self-hosting Undertone

`undertone-server` is Undertone without the window: the same library scanner, the same
OpenSubsonic API the desktop app exposes under Settings → Integrations, and the same
device sync, packaged as a console program for a NAS, a VPS or a Docker host. Any
Subsonic-compatible client (Symfonium, substreamer, Feishin, Amperfy, DSub, …) can
browse, search, stream, star and scrobble against it.

It reads and writes the exact data directory layout the desktop app uses
(`library.json`, `library.db`, `artwork/`, `playlists.json`, `server/users.db`,
`sync/`), so a data directory copied from a desktop install works as-is, and the
other way round.

## Docker

```sh
docker build -t undertone-server .
docker run -d --name undertone -p 4747:4747 \
  -v /path/to/music:/music:ro \
  -v undertone-data:/data \
  undertone-server
docker exec -it undertone /app/undertone-server user add alice
```

The container listens on plain HTTP (`UNDERTONE_TLS=0`): put a TLS-terminating reverse
proxy in front of it for anything beyond your LAN. Set `UNDERTONE_TLS=1` to let it use
its own self-signed certificate instead; the SHA-256 fingerprint is printed at start
and is what a client pins.

The first scan runs when the container starts and repeats every hour
(`UNDERTONE_RESCAN_MINUTES`). Changes inside `/music` are also picked up live where the
bind mount delivers file notifications; the periodic scan covers hosts where it does
not.

## Bare metal

Download `undertone-server-linux-x64.tar.gz` or `undertone-server-linux-arm64.tar.gz`
from a release, unpack, and:

```sh
./undertone-server user add alice --admin
./undertone-server serve --data /var/lib/undertone --music /srv/music --port 4747
```

Options fall back to environment variables when absent:

| Option | Environment | Default |
|---|---|---|
| `--data DIR` | `UNDERTONE_DATA_DIR` | `~/.config/Undertone` (Linux), `%APPDATA%\Undertone` (Windows) |
| `--music DIR[;DIR]` | `UNDERTONE_MUSIC` | the music folders saved in `settings.json` |
| `--port N` | `UNDERTONE_PORT` | `4747` |
| `--no-tls` | `UNDERTONE_TLS=0` | self-signed TLS on |
| `--rescan-minutes N` | `UNDERTONE_RESCAN_MINUTES` | `60` |
| `--no-scan` | | scan on start and periodically |
| `--no-sync` | | device sync on |

`user add` and `user passwd` prompt for the password, or take it from
`UNDERTONE_PASSWORD` for scripts. `user apikey NAME` prints a fresh API key for clients
that prefer one over a password. Passwords are stored as PBKDF2-SHA256 hashes and
never leave the server.

## What clients get

- Browse by artist, album, genre, folder; search; random songs; starred items.
- Streaming and download of the original files, with range requests for seeking.
- Cover art per album.
- Playlists: list, create, update, delete.
- Star/unstar and scrobble, written back to the library that the desktop app also reads.
- Undertone device sync (favourites, ratings, play counts, playlists; newest change wins)
  for signed-in Undertone clients.

Not there yet: transcoding on `stream` (`maxBitRate` and `format` are ignored, the
original file is sent), cover-art resizing (`size` is ignored), per-user favourites
and play counts (the library state is shared by all accounts), and a browser UI.

## Running the desktop app and the server on one data directory

Do not point both at the same directory at the same time: each keeps the library in
memory and writes it back, and the last writer wins. Use one or the other per
directory, or give the server its own copy.
