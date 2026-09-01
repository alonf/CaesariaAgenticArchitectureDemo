# Vendored Mermaid

`mermaid.min.js` is vendored so the Command Center renders the generated workflow diagram with no
network dependency: a conference room's Wi-Fi must never decide whether the demo has a diagram.

| Field | Value |
| --- | --- |
| Package | [mermaid](https://www.npmjs.com/package/mermaid) |
| Version | 11.12.0 |
| Source | `https://cdn.jsdelivr.net/npm/mermaid@11.12.0/dist/mermaid.min.js` |
| License | MIT — © 2014–2022 Knut Sveidqvist; full text in [LICENSE](LICENSE) |
| SHA-256 | `07e37dfa97b337ccc85365d57eddf99b9706f09db3b59b260d0333b23b343c4b` |

The file is stored byte-for-byte (see [.gitattributes](../../../../../.gitattributes)); do not
reformat it. To update, download the new version, refresh the version and hash above, and re-run
the Workflow-stage browser check.

Verify the hash with:

```powershell
(Get-FileHash mermaid.min.js -Algorithm SHA256).Hash.ToLowerInvariant()
```
