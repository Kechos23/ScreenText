# ScreenText development

This folder is the canonical development root. Continue work here, not in the earlier Codex or OneDrive prototype folders.

## Source and tooling

- `src/application-sources.txt` is the canonical compile/distribution source manifest for this C# WinForms/.NET Framework application. `ScreenText.cs` holds the UI; `Backend.cs`, `RequestRunner.cs`, `TransientStorage.cs`, `AppStorage.cs`, `ChatGptConnection.cs`, `AiConnectionsUi.cs`, `EditorSupport.cs`, `UiControls.cs`, `LanguageSupport.cs` and `OneOcrSupport.cs` hold shared services/controls.
- `assets/ocr.ps1` recognizes images. `assets/ocr-languages.ps1` lists recognizers without elevation and services only whitelisted Basic/OCR packs on an explicit Install action. Preserve UTF-8 BOM for Windows PowerShell 5.1.
- `scripts/IconBuilder.cs` generates the selected Text Lines icon. Editable artwork is `assets/screen-text.svg`.
- `scripts/build.ps1` and `scripts/package.ps1` implement build and packaging. Root scripts are the public entry points.
- `tests/TempAndMenuSmoke.cs`, `tests/AppearanceSmoke.cs` `tests/StartupSmoke.cs` `tests/InteractionSmoke.cs` `tests/EditorSmoke.cs` `tests/CodexSmoke.cs` and `tests/LanguageSmoke.cs` are the current offline regression harnesses. `tests/CodexProbe.cs` simulates login/request outcomes without AI usage. `tests/EditorProbe.cs` is only an executable argument recorder for editor tests. Obsolete snapshots/prototypes have been removed.
- `src/` is canonical; `outputs/` is generated and ignored. Portable releases contain runtime files only; source is distributed through the tagged repository.

## Commands

Run from this root:

```powershell
.\build.ps1
.\test.ps1
.\package.ps1
.\run.ps1
```

Default `test.ps1` runs Quick without real OCR or AI. All/Languages use installed Windows OCR and OneOCR when available. Every harness has separate preferences/fake Codex; no real language installation or AI usage. Close the app from tray Exit before Lifecycle/All or a changed-source rebuild. Unchanged Quick/UI/Reading/Translation runs may keep it open.

## Product constraints

- Keep the interface English, dark by default with optional light mode, the selected minimal Text Lines icon, and `designed by Kechos` credit. Accent color is a separate saved preference.
- Selectors use managed Control painting. Do not reintroduce native ComboBox WndProc, GetWindowDC, or non-client repaint overrides; these caused hover artifacts. Preserve keyboard and popup behavior.
- Repeated interactive launches signal the existing window through the named activation event. Automatic startup duplicates remain quiet. Preserve hotkey registration across window handle recreation.
- Close to tray defaults on. UserClosing must preserve workspace, hotkey and active requests. Tray Exit must bypass it, and Windows shutdown/Application.Exit must remain able to terminate. Test harnesses must use explicit ExitApplication for cleanup.
- Windows startup is opt-in through General. Only a user toggle writes the current-user Run entry. Tests use an isolated registry key, never the real Run entry. `--startup` must start hidden with tray and hotkey available.
- Screenshots are temporary in memory by default. Explicit Save or an explicitly enabled automatic storage preference creates permanent images. Automatic storage applies only to new region/clipboard captures, not imports or re-extraction. Never restore old screenshots automatically.
- Reload edits is intentionally removed from the app scope. Edited files can be saved by the external editor and imported with Open image.
- Codex login checks must use only login status; never infer remaining usage or server reachability from saved credentials. Keep request status separate and preserve subscription-only authentication.
- External editing uses temporary PNG copies under Windows Temp. Preserve explicit user saves during cache cleanup.
- Editor selection belongs in Settings > Editing. The main Edit button launches the selected profile directly. Preserve ShareX -ImageEditor even when selected manually; never launch ShareX with a bare image path. Search is bounded and read-only, with Browse for arbitrary portable installations. Do not silently switch to another editor when the configured one is missing.
- Recognition language, translation target and translation favorites are separate preferences. Preference changes must never generate an AI request. Missing OCR packs must preserve the captured image and source text. Language installer tests must mock servicing commands and must never install real packs.
- Keep local Windows OCR and OpenAI image extraction separate. Online requests use the independent ChatGPT plan connection; never silently switch to API-key billing or local/CLI fallback.
- Never read/import/expose authentication tokens from Codex or other applications. The independently authorized ScreenText ChatGPT OAuth service may handle only its own tokens and its CurrentUser-DPAPI vault; never log tokens or authorization/callback URLs. Do not copy personal captures, editor paths, or image history into the portable ZIP.
- Preserve right-click/Esc capture cancellation, full-resolution image copying, optional auto-copy, and editor export/Open image workflows.
- Do not make commits, publish, install dependencies, or change unrelated applications unless requested. Do not add hosted services or API requirements.

## Validation and migration

Build after application source changes (incremental `build.ps1` checks inputs/outputs). Run the relevant targeted suite: Quick for core/reading, UI for layout/theme, Storage for capture/cache/editor, Translation for simulated Codex/retention, Languages for OCR/installer mocks, Runtime for process ownership/crash recovery/large text/settings fallback, Connections for mocked ChatGPT OAuth/refresh/HTTP/UI, Lifecycle for startup/single-instance. Run All for broad validation before delivery; do not repeat All after every small UI edit. Render and inspect dark/light previews for visual changes using `-Previews`. Live Codex requests consume plan usage; run them only when needed for an authorized Codex feature test.

The earlier OneDrive prototype is not an active checkout. Do not edit or launch its backup copies.

## Automatic OCR adapter
- `src/OneOcrSupport.cs` coordinates automatic recognition and the explicit Windows OCR fallback; `src/OneOcrWorker.cs` builds an isolated x64 helper. Keep worker faults/errors out of Codex diagnostics.
- OneOCR is an unofficial integration with the current user's installed Microsoft Snipping Tool, not the public Copilot+ API. Never bundle/copy/download Microsoft models/DLLs, enumerate WindowsApps, or alter package ACLs. Resolve the registered Microsoft publisher/x64 package each request and restrict native DLL search.
- Auto · OneOCR is the first local choice. Preserve explicit Windows-language choices, saved fallback, translation prefs, and online method defaults. Fallback is local and visible; never fallback on cancellation or an empty successful result, and never auto-send to Codex.
- `tests/OneOcrSmoke.cs` covers installed-engine integration and `tests/OneOcrProbe.cs` covers failures/cancellation without AI. Probe binaries must remain excluded from portable packaging.

## Current workspace/settings UI
- Keep Extraction & Translation merged with Extraction/Translation tabs. Advanced > Windows OCR is collapsed and below ordinary extraction controls. Do not reintroduce the old account/check-login section or footer badge.
- Main local OCR offers exactly Automatic (OneOCR) and Windows OCR; retain installed-language selection in Advanced and the independent saved fallback. Codex hides local controls; keep method hints to one short line.
- Translation ellipsis stays compact at the right edge. More supports add/remove/explicit replacement of up to three favorites, saved independently without AI requests. Target lists show favorite/default markers.
- Disabled controls must be neutral across all accents/modes and must not gain accent/hover styling. `tests/WorkspaceUiSmoke.cs` covers these app-event flows and previews.

## Reading view and visual hierarchy
- Source/Translation Expand opens an owned editable resizable/maximizable window. Keep full text/RTL/copy, initial font cloned from the main editor, independent zoom, bidirectional guarded text sync and event cleanup. Editing never triggers AI. Respect the main editor read-only state during active requests. Disable Expand for empty panes.
- Keep the Extraction card subtly tinted for the active accent/mode. Empty-state body instructions are smaller and softer than their titles. Settings uses a vector three-line glyph beside its label.
- `tests/ReadingUiSmoke.cs` covers these app-event flows and previews without AI requests.

- Extraction controls fill the sidebar content width with balanced margins; keep active button/selector borders distinct and the outer extraction outline equal to the Capture region accent. The Source Translate action uses the current target and is disabled for empty source or an active request.

## Previous translation retention
- Source changes in either editor and new OCR must keep the previous translation/copy/edit state. Track its reference source and show a discreet Source changed footer when different. Never auto-translate on editing. Preserve stale state during manual translation corrections; restore freshness when source is restored exactly or a new translation succeeds.
- Keep previous result/title/RTL on failed or canceled requests. Explicit Clear still resets both editors and the reference. Expanded Translation must reflect the same stale indicator.
- Settings remains in the top header, with 110x36 size and right-edge alignment matching the right toolbar buttons. `tests/TranslationRetentionSmoke.cs` covers retention and header alignment with fake Codex only.

## Test/build/package hygiene
- Root `test.ps1` defaults to Quick and supports `-List`, targeted `-Suite`, `-Previews`, `-KeepArtifacts` and a per-harness timeout. Every harness must run independently in a fresh directory; never rely on preference files from another test. Keep GUI/clipboard/hotkey tests serial.
- `tests/CoreSmoke.cs` is the fast non-OCR core check. The existing specialized Smoke/Probe sources remain maintained. Do not replace integration coverage with only mocks; retain All and installed-OCR suites.
- Successful test/package stages self-clean; failures retain owned test artifacts. Build cache and compiled test caches are under hidden `work/` directories. Never put validation reports, probes, preferences or Microsoft model files into the portable ZIP.
- Reports are in `outputs/validation/<suite>/summary.json` plus per-harness text; previews only under `outputs/previews/<suite>/`. Read the aggregate pass/fail/hash before relying on reports.
- `docs/RELEASE-REVIEW.md` records the release review. OpenAI and GitHub Copilot are implemented; other providers remain deferred.
## Runtime repair invariants
- RequestRunner starts requests suspended, assigns an owned Windows job before resuming, and terminates the job on cancel/timeout/disposal or owner crash. Fail closed if job assignment fails; never return to parent-only cancellation. RuntimeRepairsSmoke exercises owned descendants without AI.
- TransientSession uses GUID directories, an exact ownership marker and exclusive live lease. Reclaim only unlocked known sessions; refuse unknown files, links and subdirectories. Normal capture remains memory-only; transient request inputs are not saved screenshots. Do not delete unmarked legacy Temp files.
- All main/expanded editors use TextPolicy.EditorMaximum. TranslationMaximum remains an explicit 50,000-character request limit, checked before CLI use; never silently truncate text.
- AppStorage keeps portable writable preferences, otherwise uses LocalAppData/ScreenText/Settings. Import only known preference files if absent; never auth files, screenshots, CLI hints or overwrite existing per-user choices.

## OpenAI account and models
- Settings > AI Providers uses one independent Sign in with ChatGPT connection. Account and Models tabs keep the page compact. No connection-method selector or UI calls to the legacy CLI backend remain. Main extraction is Local / AI. Do not restore a CLI/auth/billing fallback.
- ChatGptConnection retains its own OAuth PKCE/nonce/state flow, signed ID-token validation and public Responses requests with store=false/stream=true. Tokens remain CurrentUser-DPAPI protected under LocalAppData/ScreenText/ChatGPT/connection.dat, outside portable prefs/ZIP. Preserve stable host/client mapping, serialized refresh, and saved sessions across normal close/restart.
- Old Provider preferences are ignored; existing per-operation models and own encrypted login are kept. Former CLI-only users must authorize ScreenText once. Never import Codex/browser credentials or sign out other apps.
- Account/model UI changes never generate inference requests. Sign out revokes only the selected ScreenText connection and reports unconfirmed remote revocation. Network/model failures preserve existing login and committed editor content. Invalid renewal credentials may require reconnect.
- Translation and image models remain separately saved with Luna defaults; catalog/account refresh cannot reset choices. Custom IDs are bounded/validated. Presets/catalog entries are not guarantees of entitlement/image support. Models affect only ScreenText requests.
- ChatGptSmoke covers single-route UI, account identity, OAuth/DPAPI reload without browser sign-in, rotating refresh, cancellation/errors and previews with isolated mock credentials only. TranslationRetentionSmoke reuses its mock HTTP fixture; no real CLI login or AI. Legacy Backend CLI methods/probes are retained for standalone historical regression, not application routes.
- Keep mock credentials out of runtime/package. Manual live probes require an authorized feature test and consume shared Work/Codex allowance. Do not run broad live tests as routine checks.

- AI Providers settings has a managed provider selector and per-provider Account/Models panels registered explicitly. Only implemented providers appear; selecting a settings page must never connect, change the active request provider, reset models, or send AI requests. Other providers remain deferred.

## Workspace connection state
- Header shows OpenAI signed-in state and the account in its tooltip/accessibility description. Signed-in click opens AI Providers > Account; signed-out/reconnect clicks use the same OAuth flow as Settings, with cancellation and no automatic inference.
- Empty Translation is neutral/dimmed without sign-in and offers a direct sign-in button. Existing text stays readable/editable/copyable with a small sign-in-again row. Targets/favorites remain accessible; unsigned target clicks select only, and requests are gated. Network/quota/model failures do not imply logout; authorization/renewal errors can require reconnect.
- Source supports native typing/Ctrl+V, with placeholder hidden on focus and restored when empty/unfocused. No text Paste button remains. Translate belongs to Source; quick languages select only. More keeps its explicit immediate Translate action.

- Workspace sign-in labels are provider-neutral: Sign in. The header uses AccountChip with a user glyph tinted for signed-in/reconnect states, hover/focus/disabled states and an account tooltip. Keep the provider-specific OAuth wording inside the selected provider settings.

- Main extraction is Local / AI, with Via OpenAI naming the current provider. Keep the saved method values unchanged. The signed-in account chip shows provider plus a smaller muted account line (ellipsis when needed); full identity stays in tooltip/accessibility. Signed-out chip stays compact and contains no account subtitle.

- AccountChip has no signed-in checkmark or separate reconnect warning glyph. Tint the user icon using accent for signed-in, neutral for signed-out, and attention for reconnect. Text/accessibility/tooltips still explain the state. Provider name, usage hint and sign-in hint come from the active AiProviderInfo shared with its settings registration; browsing settings must not change the active provider.

- The compact Settings button is 110x36, keeps the existing right edge, and sits about 10 logical pixels after the account chip. Keep header alignment at normal/minimum window size.

## Explicit translation flow
- Quick-language clicks never send inference. The Source Translate to … action (and Ctrl+Enter in Source) uses the current target. Keep native multiline Enter and selection/caret-aware paste. More > Translate remains an explicit immediate action; favorite changes/cancel never translate.
- Keep the previous result/title on source/target changes and failures. Distinguish Source changed and Target changed, including expanded result metadata. The result title describes the actual result, not the selected future target. Empty result resets title/direction/snapshots; selected target survives Clear.

## Translation result editing
- Empty Translation is read-only and exposes no editable caret, even when signed in. Only a successfully committed translation enables editing; login or request completion alone must not unlock it. Clearing the result resets eligibility and locks both main and any open expanded editor. Retained results remain editable after source/target changes and logout.
- CommitTranslationResult is the single successful-result path. Offline visual/large-text fixtures may use it to seed an existing result. Never make editor eligibility depend on a connected account.

- Native paste/replace can emit a temporary empty TextChanged notification. Preserve existing-result eligibility and editor visibility until the edit settles; only a still-empty result loses eligibility. Explicit workspace Clear resets immediately. Cover replacing a selected result and clearing from Expand.

## Dynamic OpenAI model catalog
- Model choices come from the signed-in account catalog, followed only by retained selections/custom IDs labeled Unverified; never inject static presets into the picker. Keep independent extraction/translation defaults unchanged on refresh, removal, empty catalogs, network failures and restart. Catalog presence is not an entitlement or image-support guarantee.
- Refresh on sign-in, normal startup and explicit Refresh models/Check connection without inference. Keep the previous catalog on failed refresh; accept a valid empty catalog. Show a persistent workspace model-review notice and a settings warning when a selected ID is absent from the current account catalog. Do not silently choose a replacement, sign out on catalog/network errors, or claim the model is globally retired.

- Main workspace has synchronized extraction/translation model selectors backed by the same saved defaults and catalog as Settings. Extraction model is visible only for AI; workspace selectors are disabled while signed out, during connection work and active requests. Selection changes never generate inference or alter retained text. Prefix unverified entries with a warning triangle and retain the Unverified text in all model pickers.

- Translation model picker sits right of Copy text in the bottom actions row, without a MODEL caption; open its list upward and use an upward arrow. ThemeCombo must consume the owner click following native ToolStrip AppClicked dismissal instead of immediately reopening; keep keyboard/selection/outside-dismiss behavior and avoid double-click special handling for this toggle.

## GitHub Copilot integration
- Copilot uses the official portable CLI login and the SDK's shared CLI user authentication, explicitly authorized for this provider. Never extract, copy or expose CLI tokens. Do not restore the failed independent-profile/plaintext-consent login path.
- ChatGPT retains its own existing connection, settings and models. Active provider is saved separately; browsing provider settings never changes request routing. Use for AI explicitly changes both online extraction and translation. Never fall back to a different provider or API-key route.
- Copilot account metadata is checked on startup when active, on explicit Check/login and again before requests. Pin host and login; an external account change blocks inference until reviewed. Public github.com and validated single-tenant *.ghe.com only. Enterprise policy and deployment remain a manual environment check.
- Copilot Sign out requires a confirmation explaining that it removes the displayed account's shared CLI sign-in locally and affects other CLI consumers. It does not explicitly sign out the browser/VS Code, revoke GitHub authorization, or alter ChatGPT. Never remove all accounts.
- Keep separate per-provider translation/extraction model preferences. Copilot catalog comes from SDK listModels; Auto may be the only permitted choice. Preserve unavailable selections with warnings. No inference on settings/account/catalog changes.
- Bundle pinned official CLI, SDK and private Node runtime. No global installation or token files in portable packages. CopilotWorker.mjs is canonical; refresh runtime/source copies on build. Worker diagnostics must not expose OAuth URLs, raw native errors or tokens. RequestProcess owns the worker and all descendants for cancellation/crash cleanup.
- Copilot sessions disable tools, file hooks, memory and embedding persistence. Delete each owned SDK session on completion; retain only owned ID markers for cleanup after a crash. Never remove unrelated CLI sessions.
- CopilotSmoke uses only a fake adapter, no real auth/AI. Live feature probes require explicit task authorization and synthetic data. Record live evidence separately from offline test results.

## Public repository and release structure
- VERSION drives generated assembly/file versions, visible main/Settings version text and versioned ZIP names. No hardcoded parallel version strings in application code.
- src/ contains the maintained application; tests/ the isolated harnesses; scripts/ the build/restore/package tools; assets/ OCR helpers and artwork; runtime/copilot/ exact lockfiles/checksums. work/ and outputs/ are ignored generated locations.
- Build copies canonical assets into outputs/ScreenText and restores private pinned dependencies if absent. No global package installation. Worker-only edits do not recompile C#. Clean-clone restore must be verified before a release.
- Keep MIT for ScreenText separate from the Copilot CLI redistribution license and other third-party notices. Preserve upstream binaries and notices unchanged; do not bundle Microsoft Snipping Tool files.
- Audit the exact staged file list and release archive before publishing. Never stage caches, runtime dependencies, auth/profiles, historical live probes, private screenshots or personal migration logs.
- Release requires complete offline All validation and identical source commit/tag and ZIP/SHA256SUMS assets in GitHub/Gitea. Publishing/commits require explicit user authorization.
- LanguageSmoke runs inside a real Application.Run loop so async continuations survive modal close. Do not replace it with Application.DoEvents alone or extend timeouts to hide a context deadlock.
