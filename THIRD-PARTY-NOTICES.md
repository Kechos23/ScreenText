# Third-party notices

ScreenText source is MIT licensed. This does not relicense bundled software or grant access to any online service.

| Component | Version | License / notices |
| --- | --- | --- |
| Node.js | 22.23.4 | Node.js license and bundled third-party notices in `copilot-runtime/NODE-LICENSE.txt` |
| GitHub Copilot CLI | 1.0.105 | [GitHub Copilot CLI License](licenses/copilot-cli-LICENSE.md); unmodified executable included only as part of ScreenText |
| GitHub Copilot SDK and matching platform runtime | 1.0.28 | [MIT](licenses/copilot-sdk-LICENSE.txt); embedded third-party notices are retained in the platform package |
| Koffi | 3.2.1 | MIT; `copilot-runtime/node_modules/koffi/LICENSE.txt` and vendor notices |
| vscode-jsonrpc | 8.2.1 | MIT; `copilot-runtime/node_modules/vscode-jsonrpc/License.txt` |
| Zod | 4.6.6 | MIT; `copilot-runtime/node_modules/zod/LICENSE` |

The exact dependency tree and integrity hashes are in `runtime/copilot/package-lock.json`. Native platform dependencies include their own notices, including Microsoft OneAuth/MSAL components; all supplied notices remain in the distributed dependency tree.

Snipping Tool/OneOCR and Windows OCR are installed Microsoft components. Their binaries and models are not included. ABI references consulted during the local adapter's development: [win11-oneocr](https://github.com/b1tg/win11-oneocr) and [SnippingToolOcrSharp](https://github.com/ksasao/SnippingToolOcrSharp).

OpenAI, ChatGPT, GitHub, Copilot, Windows and other product names belong to their respective owners. Online services require their own accounts, terms and entitlements.

Zod is pinned to 4.6.6 through an npm override to address [GHSA-mrhx-c7xc-m3w5](https://github.com/advisories/GHSA-mrhx-c7xc-m3w5) in the SDK's transitive dependency. The official CLI executable remains unmodified.
