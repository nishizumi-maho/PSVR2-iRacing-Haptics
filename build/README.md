# Build

`build.ps1` places the self-contained publication under `build/portable` and
the versioned portable ZIP plus SHA-256 file directly in this folder.

After that build completes, `build-installer.ps1` compiles
`installer/PSVR2iRacingHaptics.iss` with Inno Setup 6.3 or later, performs a
silent per-user install/uninstall smoke test and writes the setup executable
plus its SHA-256 sidecar under `build/installer`.

Generated binaries are not versioned. GitHub Actions uploads all four files as
workflow artifacts, and the release workflow attaches the final four files to
the matching GitHub release.
