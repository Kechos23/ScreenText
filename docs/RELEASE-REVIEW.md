# Release review — ScreenText 1.0.0

Reviewed on 2026-11-01 for the first public GitHub/Gitea release.

## Findings fixed

- Added missing migration entries for active AI provider and Copilot model/environment preferences when the portable folder becomes read-only; existing per-user settings and all credentials remain untouched.
- Distinguished a valid empty Copilot catalog from a failed refresh. Failed same-account refresh keeps the previous catalog; removed model defaults remain selected with review warnings.
- Replaced the language harness's ad-hoc message pumping with a real WinForms application loop. Modal close no longer strands asynchronous mock HTTP continuations. LanguageSmoke passed in about 4.5 seconds in the final All run instead of timing out.
- Added one VERSION source for assembly/file versions, visible UI version and release artifact names.
- Added clean-source build/runtime restoration with pinned official downloads, SHA-256 verification, exact npm lockfile integrity and disabled npm install scripts. No global dependencies are installed.
- Removed historical live login/MCP probes, private profiles, caches from failed runs, personal migration logs, duplicate distribution source and obsolete archives. Source, tests, scripts, assets and dependency manifests are separated; generated files and credentials are ignored.
- Added MIT, independent upstream CLI/SDK licenses, notices, privacy/account documentation, synthetic screenshot, development/release instructions and semantic versioning.

- Updated the SDK host dependency Zod to 4.6.6 with an exact npm override for GHSA-mrhx-c7xc-m3w5. CLI/runtime binaries remain unmodified; npm advisory checks cover the external dependency tree, not opaque embedded vendor code.

- Removed the obsolete live --self-test/--image-test probes. Those switches now fail closed with exit code 2 instead of spending an installed Codex account's usage. Maintained tests use isolated mocks.

## Validation

- Full offline/local regression: **15/15 passed**, 110.58 seconds. This includes mocked ChatGPT/Copilot, process ownership/crash cleanup, clipboard/editor UI, temporary storage, startup/activation, language servicing mocks, installed Windows/OneOCR, reading views and translation retention.
- Tested application SHA-256: `2ce165efb367c2c0c4cd5373d2b1d17af5f248328959f635ddc78fdd24f3ea17`.
- A separate clean-source copy built successfully and restored its own pinned private runtime without using existing output binaries/dependencies.
- External npm dependency audit after the Zod override: **0 reported advisories**. A native SDK session was created successfully without login or inference using Zod 4.6.6. Pinned private npm is bootstrapped so the old global npm cannot silently ignore overrides.
- Git's exact initial staged source set was scanned with Gitleaks 8.30.1: no leaks. Generated binaries, node_modules, settings, auth/profiles and proof folders are excluded from the source commit. Release archives use an explicit runtime allowlist.
- Screenshot uses synthetic text and a fictional account. No real AI request, login, logout, language installation or Windows startup change was used as a routine release check.

## Known limits

- OneOCR is an undocumented integration with the installed Snipping Tool; compatibility depends on its version. Microsoft DLLs/models are not distributed.
- The existing ChatGPT plan connection is experimental and depends on service-side availability/entitlement.
- Public Copilot account translation/mixed-language extraction were manually proven during implementation. Enterprise Cloud deployment and actual shared-CLI logout still require manual checks in the target environment; mocks are not evidence of enterprise entitlement or remote revocation.
- The initial Windows binaries are unsigned; release SHA-256 checksums are supplied. No automatic updater is included.

No known release-blocking functional issue remained in the covered paths after these repairs. This is bounded verification, not a guarantee for all Windows installations or provider policy combinations.
