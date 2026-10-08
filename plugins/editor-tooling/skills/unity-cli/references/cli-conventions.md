# CLI conventions — flags, environment, exit codes, output

Part of the **`unity-cli`** skill. Read when scripting the CLI, parsing its JSON/NDJSON output, branching on exit codes, or configuring it through environment variables in CI.

## Global flags

These work on every command:

| Flag | Description |
|---|---|
| `--format <fmt>` | Output format: `human` (default), `json`, `tsv`, `ndjson`, `github`. Also via `UNITY_FORMAT` env var. |
| `--json` | Global shorthand for `--format json`, accepted on every command (e.g. `unity status --json`, `unity doctor --json`). `--format` takes precedence when both are supplied. |
| `--no-banner` | Suppress the branded header — use in scripts |
| `--no-pager` | Turn off paging. Governs both pagers: the external one over the long listings (`unity command`, `releases`, `editors`, `changelog`, `logs`) and the interactive one in `unity projects list`. Also via `UNITY_NO_PAGER` (presence-based — any value, including `0`, disables it). |
| `--non-interactive` | Disable all interactive prompts — use in CI |
| `--quiet` | Suppress non-essential output |
| `--verbose` | Print full error details (stack trace + cause chain) on failure. Also via `UNITY_VERBOSE`. |
| `--proxy <url>` | HTTP/HTTPS/SOCKS/PAC proxy URL for this invocation. Also via `UNITY_PROXY`. Takes precedence over standard `HTTPS_PROXY`/`HTTP_PROXY`/`ALL_PROXY` env vars and the persisted `proxy.json` setting. |
| `--proxy-disable` | Disable proxy for this invocation, ignoring all sources (env vars, persisted config, system settings). |
| `--log-proxy` | Log one redacted entry per outbound request to `proxy-request.json` — for reproducing proxy issues. Also via `UNITY_LOG_PROXY=1` or the `proxyRequestLogging` setting. |
| `--no-log-proxy` | Opt a single invocation out of proxy request logging when it's enabled globally. |
| `--color <auto\|always\|never>` | Control colored output for this invocation, overriding `NO_COLOR`/`FORCE_COLOR` and TTY auto-detection. Governs every ANSI-emitting surface (help, tables, spinners, errors), not just `human` output. |
| `--no-color` | Shorthand for `--color never`. Whichever of `--color`/`--no-color` appears last on the line wins. |

**Always use `--format json` when you need to parse output programmatically.**

`--accelerator <host:port>` and `--no-accelerator` are **not** root globals — they are accepted only on `run`, `test` and `build`, and only after the command name. See [build-run-test.md](build-run-test.md).

**`unity projects list` is the only command that pages IN-PROCESS.** It shows 10 projects per screen and waits for a keypress between screens, and only when stdout is a terminal. Paging is off for redirected stdout, under `--format json` and `--format ndjson`, and under `--all`, `--watch`, or `--no-pager` / `UNITY_NO_PAGER`.

**Not every machine format bypasses that one.** Only `json` and `ndjson` get their own non-interactive rendering; on a terminal, `--format tsv` and `--format github` fall through to the human table and page like `human` does — so `--format tsv` on a TTY yields neither TSV nor unpaged output. Redirect stdout (the usual case for a machine format) or pass `--no-pager`. Note this is the **opposite** of the external pager below, which is `human`-only: the two mechanisms differ here, and `projects list` is the surprising one.

**The long listings page through an external pager, like `git log`.** `unity command` (the bare listing), `unity releases`, `unity editors`, `unity changelog`, and `unity logs` pipe human output through `less -RFX` on a terminal — colors kept, no screen clear, and `-F` quits by itself when the output already fits one screen, so short listings show no pager UI. `$UNITY_PAGER` then `$PAGER` override the choice and run through a shell, so `PAGER="less -S"` works; a blank value is ignored rather than treated as an opt-out. Quitting with `q` exits cleanly with the command's own exit code. Unlike `projects list`'s pager this one is **`human`-only**, and it never engages for redirected stdout, any machine format (`json`, `tsv`, `ndjson`, `github`), `--quiet`, `TERM=dumb`, the streaming modes (`editors --watch`, `logs --follow`), a named `unity command <name>`, or inside `unity shell`. A broken pager costs the paging, not the output: a `$PAGER` naming something that is not there is resolved before anything spawns, and one that spawns and then dies has its output reprinted to the terminal, decided from the pager's exit status (a clean exit is a normal `q` and discards; a failure status reprints). The exception is a pager that exits *successfully* without reading — `PAGER=true`, or anything that lingers and then exits 0 — which nothing distinguishes from a `q`, and which `git` loses too. A pager that starts and merely *waits* is not treated as broken, so the CLI waits with it.

A branded Unity header (logo, wordmark, CLI version) renders on the landing surfaces — bare `unity`, `unity --help` / `-h`, `unity help`, and above the first-run consent prompt. It's shown only on a TTY, prints at most once, and degrades to compact, uncolored text on narrow terminals, without Unicode, or under `NO_COLOR`. Piped output is unaffected. Use `--no-banner` to suppress it in scripts. Bare `unity` prints usage and exits 0.

## Environment variables

All CLI env vars use the `UNITY_` prefix. A CLI flag always overrides the corresponding env var.

| Variable | Mirrors flag | Description |
|---|---|---|
| `UNITY_FORMAT` | `--format` | Output format (`human`, `json`, `tsv`, `ndjson`, `github`). `HUB_FORMAT` is a deprecated alias. |
| `UNITY_EDITOR_VERSION` | `--editor-version` | Editor version (e.g. `2023.3.0f1`, `latest`, `lts`). |
| `UNITY_ARCHITECTURE` | `--architecture` | Chip architecture (`x86_64`, `arm64`). |
| `UNITY_PROJECT_PATH` | path argument | Project path — used by `open`, and also honored by `status` and the cloud commands. |
| `UNITY_QUIET` | `--quiet` | Suppress non-essential output. |
| `UNITY_VERBOSE` | `--verbose` | Show full error details on failure. |
| `UNITY_NON_INTERACTIVE` | `--non-interactive` | Disable interactive prompts. |
| `UNITY_NO_BANNER` | `--no-banner` | Suppress the branded banner. |
| `UNITY_NO_PAGER` | `--no-pager` | Turn off paging — both the external pager over the long listings and `unity projects list`'s interactive one. Presence-based: any value counts, including `0`. |
| `UNITY_PAGER` | — | The pager to use for the long listings, overriding `$PAGER` and the `less -RFX` default. Runs through a shell, so flags work (`less -S`). A blank value is ignored, not an opt-out. |
| `PAGER` | — | Same as `UNITY_PAGER`, consulted only when that is unset or blank. |
| `LESS` / `LV` / `LESSCHARSET` / `MORE` | — | Passed to the pager only when you have not set them, defaulting to `FRX`, `-c`, `utf-8`, and `FRX`. `LESSCHARSET` keeps multi-byte glyphs readable where the locale does not declare UTF-8; `MORE` exists because `more` on macOS/BSD is `less` under another name and reads `$MORE`, so without it `PAGER=more` waits for a keypress even for one line. |
| `UNITY_RUN_TIMEOUT` | `--timeout` | Timeout for `unity run` in seconds. |
| `UNITY_TEST_TIMEOUT` | `--timeout` | Timeout for `unity test` in seconds. |
| `UNITY_CLOUD_ORG` | `--cloud-org` | Active Unity Cloud organization id or name for a single call. |
| `UNITY_CLOUD_PROJECT` | `--cloud-project` | Cloud project ID; used by Cloud Build inventory. Pipeline Automation inventory is organization-scoped. |
| `UNITY_SERVICE_ACCOUNT_ID` | — | Service account client ID for non-interactive (CI) auth. |
| `UNITY_SERVICE_ACCOUNT_SECRET` | — | Service account client secret for non-interactive (CI) auth. |
| `UNITY_PROXY` | `--proxy` | HTTP/HTTPS/SOCKS/PAC proxy URL. Takes precedence over `HTTPS_PROXY`/`HTTP_PROXY`/`ALL_PROXY` and the persisted `proxy.json` setting. |
| `UNITY_NO_UPDATE_CHECK` | — | Disable the background "update available" check (see `unity config update-check`). |
| `UNITY_NO_CONSENT_PROMPT` | — | Suppress the one-time first-run analytics consent prompt *without* recording a choice — for wrapper scripts on an interactive terminal that must never absorb the prompt. Analytics stay off until you run `unity analytics opt-in`. Unlike `UNITY_NON_INTERACTIVE`, it changes nothing else about command behavior. |
| `UNITY_NO_CRASH_REPORT` | — | Disable anonymous crash/error reporting (Sentry) entirely. |
| `UNITY_LOG_PROXY` | `--log-proxy` | Log one redacted entry per outbound request to `proxy-request.json`. Truthy values: `1`, `true`. |
| `UNITY_ACCELERATOR` | `--accelerator` | Unity Accelerator endpoint (`host:port`). Outranks the persisted `accelerator.json`; `--accelerator` outranks it. |
| `UNITY_NO_ELEVATE` | `--no-elevate` | Windows: skip the elevated (UAC) install helper for `install` / `install-modules`, so the install service runs unelevated. The Editor's NSIS installer still asks for elevation on demand if Windows requires it for your account — an administrator token always does; a standard user never does. |
| `UNITY_INSTALL_RETRIES` | `--retries` (`install-modules` only) | Number of times `install` and `install-modules` retry an editor or module download whose transfer or validation fails. `0` disables retries; `unity install` has no `--retries` flag, so set the variable there. |
| `UNITY_NO_AUTH_BROKER` | — | Skip the resident auth broker and read credentials directly from the OS keyring. By default every command that needs a token goes through a broker that starts on demand and exits after two idle minutes (see [auth-license-cloud.md](auth-license-cloud.md)). |
| `UNITY_PEER_AUTH_MODE` | — | How the auth broker and the Editor identity helper verify a connecting process’s code signature. `enforce` is the default on macOS and Windows: an unsigned or non-Unity-signed peer is refused. `identify-only` logs without refusing — use it for an Editor you built from source. Linux logs only unless set to `enforce` together with `UNITY_PEER_AUTH_LINUX_ALLOWED_HASHES` (comma-separated SHA-256 hashes of trusted executables). |
| `UNITY_CLI_HOME` | — | Install root for the install script and `unity self-install`, on every platform including Windows: the binary lands in `<UNITY_CLI_HOME>/bin` instead of the default location. |
| `UNITY_CLI_FTUE` | — | Agent first-session ("paved") mode. Any value except empty or `0` turns it on. Every `--format json` envelope and ndjson `result` frame then carries a top-level `"paved": true`, and `projects create` saves the choice to the new project's `UserSettings/UnityCliPaved.json`, so later commands run in that project are paved with no variable set. Nothing else changes. See [projects-templates.md](projects-templates.md). |
| `UNITY_NO_EDITOR_IDENTITY_SERVER` | — | Disable the background identity helper that `unity open` starts to answer the Editor’s sign-in lookups when no Hub is running (see [projects-templates.md](projects-templates.md)). Presence-based. |
| `UNITY_EXPERIMENTAL_FEX_EMU` | none | Linux arm64 only, experimental and unsupported: run the x86_64 Editor under FEX-Emu. `1`, `true` or `yes` turns it on, and it takes effect only when FEX-Emu and an x86_64 RootFS are installed (`unity doctor` checks). C# script compilation is known to crash under FEX-Emu. See [editors-install.md](editors-install.md). |

**CI service account auth:** Set both `UNITY_SERVICE_ACCOUNT_ID` and `UNITY_SERVICE_ACCOUNT_SECRET` to skip the browser OAuth flow — this keeps the secret out of the process argument list and shell history. These map to the `--client-id` / `--secret-from-stdin` inputs of `unity auth login`, but reading the credentials from the environment isn't a full login: it doesn't run the interactive flow or persist credentials to the keyring.

## Exit codes

| Code | Meaning |
|---|---|
| 0 | Success |
| 1 | General error |
| 2 | Bad arguments |
| 3 | Authentication failure |
| 4 | Precondition not met (e.g. no license active, floating server not configured) |
| 6 | Command-specific failure |
| 7 | Network or transient service failure for cloud automation inventory (see its reference for exact mappings). |
| 8 | `unity test` only — the tests ran and one or more **failed**. Every other way a test run fails (compile error, unavailable license, editor crash, `--timeout`) keeps `6`, so CI can retry an infrastructure failure and never retry a failing test. |
| 9 | `unity install`, `unity install-modules` and `unity projects require` only: `--no-wait` or `--wait-timeout` ran out while the Hub or another CLI held the shared install lock (`INSTALL_LOCK_BUSY`). Retry later. Earlier items of the same run may already be installed; see `data.completedUids`. |
| 130 | Interrupted — Ctrl+C / SIGINT (128 + 2) |
| 143 | Terminated by SIGTERM (128 + 15) — e.g. `kill` or a CI/runner timeout. Emitted by long-running commands that install a signal handler to clean up first (currently `unity build`, which scrubs the temporary Android keystore). |

The `cloud` and `auth` commands map an authentication failure (expired/missing session, rejected sign-in) to `3`, and any other operational failure (network, server error) to `6` — so scripts can reliably tell "sign in again" apart from a genuine command failure.

## Notifications

A `--format json` or `--format ndjson` envelope may carry a `notifications`
array: advisories about the user's environment rather than about the command
you ran. **When one is present, tell the user** — they have no other way to see
it, because the human-facing equivalent is a terminal banner that never reaches
you.

The key is absent when there is nothing to report, so read it as
`envelope.notifications ?? []`.

```jsonc
{
  "code": "CLI_UPDATE_AVAILABLE",
  "message": "A new version of the Unity CLI is available: 1.0.0-beta.8 → 1.0.0-beta.9",
  "data": { "current": "1.0.0-beta.8", "latest": "1.0.0-beta.9" },
  "remediation": { "command": "unity self-update", "requiresUserApproval": true }
}
```

Branch on `code`; `message` is localized and meant for display. `data` is
shaped per `code`.

`remediation` says how the notification gets resolved. It may be absent, which
means the notification is informational and there is nothing to run.

`FEX_EMU_EXPERIMENTAL` means the command ran, or will run, the x86_64 Editor
through FEX-Emu on Linux arm64. Tell the user it is experimental and
unsupported and that C# script compilation is known to crash there.

**`requiresUserApproval: true` means do not run `remediation.command`
yourself.** Surface the notification and the command, and let the user decide.


## Notes

- `--non-interactive` and `--yes` together suppress all prompts — use both in CI.
- `--format json` always produces machine-readable output; prefer it over parsing human text. Error envelopes are pretty-printed with the same 2-space indent as success envelopes.
- **Read failures from stdout, not stderr.** A failed command still writes a complete document to stdout: under `--format json` an envelope with `success: false` and a populated `errors` array (`errors[0].code` is the stable token to branch on); under `--format ndjson` the usual terminal `{"type":"result","success":false,…}` frame. **Every ndjson stream ends with exactly one terminal `{"type":"result"}` frame, on success too**, so a stream without one was truncated. Table-shaped listings (`unity editors`, `unity editors upgrade --check`, `unity templates list`, `unity releases`, `unity projects list`, `unity context list`, `unity auth list`, `unity modules list`, `unity list`, `unity doctor`) write one row per line first, with no `type` field on the rows, then a terminal frame whose `data` is `{"count": N}`; `--quiet` drops the rows but keeps the frame (except `unity projects list`, which never quiet-gates its rows), and a `--watch` listing, which only ends on Ctrl-C, writes no terminal frame. A few commands (`unity env`, `unity version`, `unity pipeline list`, `unity install-modules --list`, `unity changelog` among them) still end a successful ndjson stream without one, which is a known bug. **Branch on `success`, never on `data`** — `data` is usually `null` on a failure, but not always: a partial `unity editors add` failure carries a row per path, and an ambiguous `unity auth switch` carries `data.candidates` for you to disambiguate with. Check `success` and the exit code — never treat empty stdout as a failure signal, and do not parse stderr, which carries only human diagnostics in these formats. A handful of commands have not migrated yet and still print `{"error": "…"}` to stderr with empty stdout; if stdout is empty on a non-zero exit, that is a known bug in that command rather than a shape you should code against.
- `unity <version> [path]` is a shorthand for `unity open [path] --editor-version <version>`. Works with `lts`, `latest`, or a full version string like `6000.0.47f1`.
- The CLI supports kubectl-style plugins: any `unity-<name>` binary on PATH is callable as `unity <name>`.
- Terminal output is hardened against control-character / escape-sequence injection from server-provided values (project titles, editor versions, module names) — C0 controls and non-SGR escape sequences are stripped from table/list/tree output, and now also from Commander usage errors, the `unity bug` log-archive warning, and `unity projects add`/`remove` machine (tsv) output, while SGR color/style codes are preserved.
- The CLI reports anonymous crashes and errors via Sentry to help fix bugs (no IP address or hostname; home-directory paths and token-like values scrubbed before send), aligned with the Unity Hub. Opting in to analytics additionally attaches an anonymized machine id; opted-out users stay fully anonymous. Set `UNITY_NO_CRASH_REPORT` to disable reporting entirely. Separately again, every run sends one anonymous `cli telemetry` usage ping regardless of analytics/consent state — see [diagnostics-maintenance.md](diagnostics-maintenance.md#analytics--usagetelemetry-consent).
- The CLI is currently in **beta** (latest: `1.0.0-beta.11`). It moved to 1.0 versioning at `1.0.0-beta.1`; it's still a beta. The install command needs no channel setting: until GA ships it installs the latest beta, and afterward the stable release.
- **Always use the latest version of the Unity CLI unless you are told to use a specific version.** A newer CLI regularly adds commands and output fields these docs describe, so an outdated one fails in ways that read like the docs being wrong.
- As of `0.1.0-beta.8` the CLI checks in the background for a newer version and prints an unobtrusive "update available" notice (interactive sessions only; never delays a command). Turn it off with `unity config update-check off` or the `UNITY_NO_UPDATE_CHECK` env var.
- **Under `--format json` and `--format ndjson` that same notice reaches you as a `notifications` array on the envelope** — see [Notifications](#notifications) above. Over MCP it arrives once per session, as prose in the server's `initialize` `instructions`.
- Outbound HTTP from every CLI command honors the resolved proxy (see `unity config proxy`). An invalid `--proxy` value (malformed URL or unsupported scheme) fails with a usage error (exit 2) instead of being silently ignored. Inspect what the CLI actually resolved with `unity env --format json` or `unity doctor --format json` — both surface the active proxy URL, its source, and auth source.
