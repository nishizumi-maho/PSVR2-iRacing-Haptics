# Build

`build.ps1` places the self-contained publication under `build/portable` and
the versioned ZIP plus SHA-256 file directly in this folder. Generated binaries
are not versioned; GitHub Actions uploads them as workflow artifacts and the
release workflow attaches the final pair to the matching GitHub release.
