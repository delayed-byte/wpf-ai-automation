# Implementation Progress

Last updated: 2026-07-31

## Completed

### Phase 1 — Bootstrap solution and contracts

- Solution projects, central package management, analyzers, contracts, schemas,
  sample configuration, fixtures, and validation are in place.
- Contract and schema validation run as part of the unit-test suite.

### Phase 2 — Process, session, and inspection support

- The allowlisted Patient Demo catalog, serialized FlaUI session lifecycle,
  exact element resolution, bounded inspection, and configured data redaction
  are implemented.
- The local inspection harness repeatedly launches, inspects, and closes the
  configured Patient Demo without exposing arbitrary process execution.

### Phase 3 — Actions, waits, assertions, and evidence

- Semantic text entry, invoke, selection, reads, monotonic condition waits, and
  assertion results are implemented over the live session boundary.
- Evidence runs write flushed JSONL events, final run summaries, correlated
  before/after snapshots, and failure screenshot references.
- Screenshots use conservative full-frame masking whenever a configured
  sensitive control is present, avoiding DPI-dependent redaction gaps.
- Deterministic fault injection supports resolution, timeout, and screenshot
  failure-path testing.
- The fixed local Patient Search harness verifies both a successful search and
  a deliberately failing assertion without fixed application sleeps.

### Phase 4 — Plan execution and MCP tools

- Whole-plan validation enforces configured step, string, individual-timeout,
  total-timeout, and UI-tree ceilings before application launch or mutation.
- Declared `${name}` test-data variables resolve deterministically; undeclared
  variables and expression-like placeholders are rejected.
- The plan runner executes sequentially, fails fast, records skipped steps,
  captures configured failure evidence, and closes/finalizes with an uncancelled
  cleanup path when execution or an MCP connection is cancelled.
- The local stdio MCP server exposes exactly the twelve allowlisted semantic
  tools. Generated schemas reject additional members and expose no shell,
  executable-path, PID, filesystem, or coordinate capability.
- JSON Lines evidence now writes exactly one compact event object per line.

## Verification

The latest Phase 4 verification passed:

```powershell
dotnet test WpfAiAutomation.slnx --no-restore -c Release
```

Results: 33 unit tests, 1 integration test, and 1 Patient Demo test passed.

An MCP stdio transport smoke test successfully initialized the server, listed
the allowlisted tool schemas, and called `execute_plan` against the configured
Patient Demo. The plan set `PM-1001`, invoked Search, waited for the result, and
asserted `1 patient found.`; all 4 steps passed and the evidence run finalized.
A separate disconnect smoke test launched Patient Demo, closed the MCP client's
stdin, and verified that both the server and allowlisted application process
exited without leaving an orphan.

The local harness commands are:

```powershell
dotnet run --project apps/AgentServer -- run-patient-search
dotnet run --project apps/AgentServer -- run-patient-search-failure
dotnet run --project apps/AgentServer -- mcp
```

The second command returns `AssertionFailed` and emits complete failed-step
evidence with a redacted screenshot artifact.

## Next

Phase 5 will add Patient Search Page Objects, reviewed deterministic regression
tests, and the quarantined generation/review workflow.
