# Architecture and limitations

ScreenText is an x64 C# WinForms application compiled with the .NET Framework compiler. `src/application-sources.txt` is the compile manifest. Capture uses its own region picker; ShareX is optional editing software, not a capture dependency.

## Recognition

Automatic OCR discovers the current user's registered Microsoft Snipping Tool x64 package on each operation. A separate worker loads the installed OneOCR engine in place. No WindowsApps enumeration, package ACL changes, copied Microsoft binaries or downloaded models are needed. Incompatibility is isolated from the UI and falls back locally to the configured Windows recognizer. An empty successful recognition or cancellation never falls back to AI.

Windows OCR recognizes with one installed engine at a time. The legacy Greek/English mode combines local passes; it is not a universal multilingual engine. Language-pack installation is explicit, whitelisted and can require administrator permission/company servicing policy.

## Provider routing

The active provider is explicitly saved, independently of each provider's two model defaults. Settings browsing, account checks and model changes do not trigger inference. Main workspace selectors and settings share the same saved choices. No request can silently fall back to another provider or an API key.

The existing independent ChatGPT OAuth/PKCE/nonce flow validates signed identity tokens and protects its own saved session with CurrentUser DPAPI. No browser or Codex credentials are imported. Responses requests use the selected model explicitly and disable server conversation storage. This plan connection is experimental rather than a promise of a permanent public ChatGPT-chat automation API.

The optional OpenAI Codex CLI method uses official app-server JSON-RPC for account/read, browser login, confirmed shared logout, paginated model/list and text/image turns. No token files are read or copied. The installed standalone executable is discovered through PATH, the standard npm package or the desktop runtime. Missing/incompatible CLIs produce a connection error; no global install/update is performed. Separate method preferences preserve Direct defaults. Before each request, the displayed email/plan/workspace metadata is checked again; the current protocol does not expose a stable account/workspace ID, so this is a metadata comparison, not a cryptographic identity binding. Requests use ephemeral sessions with explicit models, read-only access, disabled shell/plugins/apps/hooks/memory, disabled configured MCP servers, scoped image files and owned process jobs. Managed requirements remain authoritative; unsupported policies/configurations can prevent requests.

Copilot uses official CLI browser sign-in plus the SDK's authenticated-user route. Environment token/API-key overrides are removed. Host/login are checked before each inference; mismatch requires an explicit account refresh. Per-account SDK logout is confirmed because it changes the shared local CLI account. It does not remotely revoke OAuth authorization or explicitly sign out VS Code/the browser. Enterprise Cloud code paths are implemented but require a real managed-environment deployment check.

Valid empty catalogs and refresh failures are distinct. Failed catalog checks retain previous same-account models. Removed model IDs remain selected and are marked unverified; they never silently change. Account metadata/catalog presence are not proof of remaining usage or image support.

## Local storage and processes

Normal captures are memory-only. Explicit/automatic saves survive Clear/exit. External editing and request input files use scoped temporary storage. Request processes are assigned to owned Windows jobs before resuming; cancellation, timeout or owner exit kills descendants. Only marked, unlocked, known temporary sessions are reclaimed.

Copilot requests disable tools, configuration discovery, hooks, skills, memory, session-store/embedding persistence and host Git operations. Owned session-ID markers allow SDK cleanup after a crash; unknown CLI sessions are never removed. Provider-side retention follows provider policy.

Signed-out empty Translation stays inactive. Existing results remain editable/copyable after source changes, errors or logout; successful explicit translation is the only path that unlocks an empty result editor. Emptying the result resets its title and edit eligibility. Expanded editors synchronize guarded edits with the main panes.
