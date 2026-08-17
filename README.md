# LogSentinel

Nightly log scanner. C# app orchestrates, PowerShell (`scripts/Search-Logs.ps1`) does the
actual file/content search, and (optionally) a headless Claude Code invocation investigates
any issue found and produces a fix/diagnostics plan — it never edits code.

## How it works

1. `dotnet run --project src/LogSentinel -- --config config/log_sentinel.config.json`
2. For each `log_dirs` entry: shells out to `Search-Logs.ps1`, which scans files under
   `path` modified in the last `days_to_check` days for the configured `patterns`, and
   returns matches as JSON.
3. If issues are found and `claude.enabled` is true: launches `claude -p` headlessly with
   its working directory set to that entry's `codebase_path`, restricted to
   `Read,Grep,Glob` (read-only — it cannot edit anything), asking it to find the root
   cause in the actual code, or — if not obvious — propose what logging/tests would
   narrow it down, or — if the issue looks transient — recommend a retry/back-off
   strategy instead of a code fix.
4. Writes one markdown report per log dir to `reports/`, plus a `reports/summary_*.md`,
   and appends one line to `logs/run_log.jsonl` for history/audit.
5. If `handoff_doc_path` is set, rewrites a marker-delimited section
   (`<!-- LOGSENTINEL:START -->` … `<!-- LOGSENTINEL:END -->`) inside that file with the
   current run's findings — the rest of the doc is left untouched. This is a **replace**,
   not an append: resolved issues disappear from the doc on the next run instead of
   piling up. If the doc doesn't have the markers yet, they're added to the end; if the
   doc doesn't exist, it's created.

## Setup

```powershell
cp config\log_sentinel.config.example.json config\log_sentinel.config.json
# edit log_sentinel.config.json — log_dirs, patterns, days_to_check, etc.
dotnet build
dotnet test
```

First run with `claude.enabled: false` to sanity-check the scan itself:

```powershell
dotnet run --project src\LogSentinel -- --config config\log_sentinel.config.json
```

Then flip `claude.enabled: true` once you're happy with what's being flagged.

## Config (`config/log_sentinel.config.json`)

| Field | Meaning |
|---|---|
| `days_to_check` | How many days back to scan (by file `LastWriteTime`) |
| `context_lines` | Lines of context captured before/after each match |
| `report_dir` / `run_log_dir` | Output locations, relative to the repo root |
| `handoff_doc_path` | Optional. Absolute path to a markdown doc (e.g. a project's `HANDOFF.md`) whose managed section gets rewritten each run. Omit/null to disable. |
| `claude.enabled` | Whether to invoke Claude when issues are found |
| `claude.timeout_seconds` | Kill the Claude process (and its tree) if it runs longer than this |
| `claude.allowed_tools` | Tools Claude is permitted to use — keep this `Read,Grep,Glob` (no edit tools) |
| `claude.max_issues_per_prompt` | Cap on issues bundled into one Claude prompt per log dir |
| `patterns[]` | `{ name, regex, severity }` — matched via PowerShell `Select-String` |
| `log_dirs[]` | `{ name, path, codebase_path, file_filter, recurse }` — one entry per project to watch |

`codebase_path` matters: it's the working directory Claude gets, so it can actually read
the code that produced the log line rather than reasoning from the excerpt alone.

## Scheduling

`scripts/Register-ScheduledTask.ps1` creates a nightly Windows Scheduled Task. It is
**not run automatically** — review it and run it yourself once you've published the app:

```powershell
dotnet publish src\LogSentinel -c Release -o publish
powershell -File scripts\Register-ScheduledTask.ps1 -Time 03:00
```

## Tests

`dotnet test` runs the xUnit suite in `tests/LogSentinel.Tests`. `PowerShellRunner` and
`ClaudeInvoker` shell out to real external processes and aren't unit tested directly —
`Program.cs`'s orchestration is exercised through the `IPowerShellRunner`/`IClaudeInvoker`
interfaces so it can be tested with fakes; the process-launching implementations are
covered by the manual smoke test above instead.
