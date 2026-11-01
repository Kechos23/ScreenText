# Development and releases

## Layout

- `src/`: maintained C# application/worker sources, Copilot IPC worker and compile manifest.
- `tests/`: isolated smoke harnesses, mock HTTP adapters and fake child executables.
- `scripts/`: compiler, dependency restore, packaging and icon source.
- `assets/`: icon/SVG, OCR PowerShell helpers and sanitized product screenshots.
- `runtime/copilot/`: dependency lockfiles and pinned runtime checksums; no executables in Git.
- `licenses/`: required third-party license copies.
- `work/`, `outputs/`: ignored generated caches, test staging and build/release artifacts.

## Build

Use Windows x64 with .NET Framework 4.8 and its compiler. The default build restores private Node 22.23.4, Copilot CLI 1.0.105 and SDK 1.0.28 when missing. Pinned private npm 10.9.4 is bootstrapped and integrity-checked for restore; no global Node/npm is needed. Windows tar.exe extracts it. Official binary SHA-256 values and npm lockfile integrity are enforced; install scripts are disabled and no global installation/configuration is changed.

`build.ps1 -SkipRuntime` compiles local/UI code without downloading Copilot dependencies. `scripts/restore-runtime.ps1 -Force` restores the pinned runtime. Do not update the SDK, platform runtime and CLI independently without testing the resulting combination.

`VERSION` is the single `major.minor.patch` source. Build generates assembly/file/informational attributes; the main window subtitle and Settings display it. Keep the main activation title stable. A JavaScript-worker-only change does not recompile C#.

## Testing

`test.ps1` defaults to Quick. `-List` lists suites; `-Previews` exports synthetic dark/light images. Tests use fresh per-harness folders, mocked credentials and isolated startup registry entries. They never sign in, use real accounts, send live AI or install language packs. UI/clipboard/hotkey tests run serially. Only Languages/All run installed local OCR checks. Close the real app through tray Exit before rebuilding or running Lifecycle/All.

Test/cache artifacts are ignored. Successful staging is removed; failed staging is retained for diagnosis. Do not publish test fixtures, runtime profiles, personal screenshots or user settings. Live provider tests are separate, explicitly authorized checks and must not be described as offline regression coverage.

## Release

1. Update `VERSION`, release notes, screenshots/notices when needed.
2. Run `build.ps1`, targeted checks, then `test.ps1 -Suite All -Previews` once for the release candidate.
3. Run `package.ps1`. Outputs: `outputs/releases/ScreenText-<version>-win-x64.zip` and `SHA256SUMS.txt`.
4. Audit the Git index and archive allowlist. No binaries/dependencies, auth files, profiles, logs or personal data enter Git. The portable ZIP includes only runtime assets, licenses and documentation; source comes from repository/tag archives.
5. Commit the reviewed source, tag `v<version>`, and push the same commit/tag to GitHub and Gitea. Upload the identical ZIP/checksum assets to both releases and verify remote hashes.

Use semantic versioning for future releases. Binaries are currently unsigned; publish SHA-256 checksums and avoid claims of universal OneOCR or corporate Copilot compatibility.
