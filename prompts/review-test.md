# Review a generated Patient Demo regression-test proposal

Review the quarantined proposal against its successful plan, plan result,
sanitized snapshots, and the hand-written Patient Search reference test. Reject
the proposal if any answer below is no or cannot be established.

## Review checklist

- The test name and `Smoke`/`Regression` category are stable and descriptive.
- Assertions exactly preserve the behavior proven by exploratory evidence.
- Test data is deterministic, isolated, fictional, and reset by fixture scope.
- Test methods use a reviewed Page Object and do not access FlaUI directly.
- Locators are exact, unique AutomationIds contained inside Page Objects.
- Every asynchronous transition uses a condition-based bounded wait.
- There are no sleeps, coordinates, global input, timestamps, random IDs,
  absolute paths, arbitrary processes, shell commands, or filesystem access.
- The workflow is read-only/normal; no restricted mutation is present.
- Failure evidence is captured and fixture cleanup always closes the process.
- The proposal builds in isolation and adds behavior not already covered.
- Any shared Page Object change is reviewed separately before test promotion.

Return `APPROVE FOR HUMAN PROMOTION` only when every item passes. Otherwise
return `REJECT`, list each concrete finding, and cite the offending code. Human
approval and moving the file out of `tests/Generated` remain separate actions.
