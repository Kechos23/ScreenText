# ScreenText 1.1 review

## Connection ownership

OpenAI Direct retains its existing independent OAuth/DPAPI session. Codex CLI is explicitly selected and uses official app-server RPC methods; credentials are never imported from files. Copilot routing/authentication is unchanged. No automatic fallback exists. The connection method is saved independently of the active provider, with separate model preferences for Direct and CLI. These preference files participate in read-only-folder migration.

CLI account identity comes from account/read (email, plan and optional workspace name). It is checked before requests and before logout. The current protocol does not expose a stable account/workspace ID; metadata comparison cannot distinguish two workspaces with identical displayed metadata. External sign-in changes require Check connection to review the new account. Shared CLI logout requires confirmation; Direct and Copilot are not signed out.

## Request scope

App-server is launched under an owned Windows job. Shell, unified execution, hooks, plugins, apps, memories, browser and multi-agent functionality are disabled. Configured MCP entries are individually disabled for the request; an empty table alone would merge with user configuration and is insufficient. No global/user CLI configuration is written. Managed requirements remain authoritative and may prevent requests. Sessions are ephemeral and read-only; images are scoped temporary files. Unexpected tool requests are rejected. Cancellation/timeout/process exit terminates descendants.

Catalog loading is paginated. Valid empty catalogs differ from failed refreshes; failures preserve the previous same-account catalog. Missing/custom defaults remain unverified, without silent replacement. API-key and other provider authentication are rejected for this connection.

## Validation

CodexCliSmoke exercises the real IPC transport against a fake child executable: browser login, rejected login URLs, account metadata, pagination, explicit models, text/image routing, identity/workspace changes, API-key refusal, empty/failed catalogs, cancellation, failed turns, tool rejection, guarded logout and independent saved defaults. UI fixtures cover account/header identity, routing, method/provider changes and dark/light layouts. Existing Direct and Copilot suites remain part of All.

Separate live checks (2026-11-01) identified a real CLI account, returned eight models, translated synthetic English text to German, and extracted `Καλημέρα — Hello 123` from a generated image using gpt-6-luna. They used plan allowance. No real login/logout was performed by automated tests. Corporate OpenAI access is still unverified; this release does not promise to avoid an administrator approval requirement.

## Distribution

Version 1.1.0 is generated from VERSION. The official Codex CLI is discovered on the destination PC and is not redistributed in this release; the existing pinned Copilot runtime is unchanged. No new global installation, API key, hosted service or credential file is added to the portable package. SHA-256 checksums accompany the Windows x64 archive.

Final release-candidate regression: **16/16 passed**, 150.48 seconds. Clean staged source compiled as 1.1.0.0; exact staged-source Gitleaks scan reported zero leaks. The archive executable matches the fully tested application hash.
