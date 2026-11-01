# Security

Do not report credentials, authorization URLs, screenshots containing private text or saved account profiles in public issues. Use GitHub's private vulnerability reporting when available, or contact the maintainer through their profile without including secrets in the first message.

ScreenText stores its own ChatGPT connection with Windows CurrentUser DPAPI; Copilot uses official CLI-managed credentials. No credentials belong in the source repository or release package. Report provider isolation, unsafe process lifetime, unintended file cleanup, token disclosure and screenshot retention bugs as security issues.

Only the latest released version is supported. There is no automatic updater or code-signing certificate in the initial release.
