# Packaging

Source-of-truth package-manager manifests for Undertone.

| Dir          | Manager     | Distributed via                                   | Consumes                       |
|--------------|-------------|---------------------------------------------------|--------------------------------|
| `scoop/`     | Scoop       | `heartached/scoop-bucket` repo                    | `Undertone-windows-x64.zip`       |
| `winget/`    | winget      | PR to `microsoft/winget-pkgs` (`heartached.Undertone`)| `Undertone-v{ver}-Setup.exe`      |
| `chocolatey/`| Chocolatey  | `community.chocolatey.org` (`undertone`)             | `Undertone-v{ver}-Setup.exe`      |

On every published GitHub Release, `.github/workflows/package-managers.yml`
refreshes the Scoop manifest in the bucket repo and opens the winget PR.
Chocolatey is currently published manually — see `docs/RELEASE-PACKAGING.md`.
