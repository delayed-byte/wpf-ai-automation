I launched `patient-demo` and inspected the live UI tree first, so the proposal below targets the actual AutomationIds rather than guesses. The session is still open (`c7d3cd12…`) if you want to run any of these.

## What the app exposes

| Area | Elements |
|---|---|
| Editor | `PatientIdTextBox`, `FirstNameTextBox`, `LastNameTextBox`, `DateOfBirthDatePicker` (with child `PART_TextBox` / `PART_Button`) |
| Validation | `PatientIdValidationMessage`, `FirstNameValidationMessage`, `LastNameValidationMessage`, `DateOfBirthValidationMessage` |
| Commands | `SearchButton`, `AddButton`, `ClearButton` (enabled), `UpdateButton`, `DeleteButton` (disabled at startup) |
| Output | `PatientResultsGrid` (Selection pattern, 3 seeded rows), `StatusMessage` (currently "Ready") |

## Proposed suite

**P0 — Smoke & initial state**

| ID | Test | Assertion |
|---|---|---|
| T-001 | App launches | `MainWindow` exists; `StatusMessage` = "Ready" |
| T-002 | Command enablement at rest | Search/Add/Clear `enabled`; Update/Delete **not** enabled |
| T-003 | Clean editor | All four inputs `value` = ""; all four validation messages empty |

**P0 — Validation** (the highest-yield area, since every rule has a dedicated message element)

| ID | Test | Expected |
|---|---|---|
| T-010 | Add with everything blank | All four validation messages `notEmpty`; no row added |
| T-011 | Blank Patient ID only | `PatientIdValidationMessage` notEmpty, other three stay empty (catches over-eager validation) |
| T-012 | Malformed Patient ID (`abc`, `12`, `P-!!`) | Rejected with message |
| T-013 | Duplicate Patient ID of a seeded row | Rejected; `StatusMessage` reports the conflict |
| T-014 | Blank first / last name | Field-specific message |
| T-015 | Name at max length and max+1 | Boundary — accept / reject |
| T-016 | Legitimate awkward names (`O'Brien`, `Anne-Marie`, `Muñoz`) | **Accepted** — negative-of-the-negative test |
| T-017 | Blank DOB | Field-specific message |
| T-018 | Future DOB | Rejected |
| T-019 | DOB = today | Boundary, accepted |
| T-020 | DOB in 1850 | Business-rule bound |
| T-021 | Garbage typed into `PART_TextBox` (`13/45/2020`) | Graceful rejection, no crash, no silent coercion |
| T-022 | Fix an invalid field, re-submit | Message clears — validation state isn't sticky |

**P0/P1 — CRUD & selection**

| ID | Test | Expected |
|---|---|---|
| T-030 | Add valid patient | `StatusMessage` confirms; row appears; editor resets per spec |
| T-031/032 | Search by exact ID / partial last name | Grid filters; status reports count |
| T-033 | Search with no matches | Empty grid; explicit "no results" status, not silence |
| T-034 | Search with all criteria blank | Defined behaviour (all rows vs. prompt) |
| T-035 | Select a grid row | Update/Delete become enabled; editor populates |
| T-036 | Update selected | Status confirms; grid reflects the change |
| T-037 | Delete selected | Row removed; Update/Delete revert to disabled |
| T-038 | Clear | Inputs empty, messages empty, selection dropped, Update/Delete disabled, status "Ready" |
| T-039 | Double-click Add rapidly | No duplicate record (double-submit guard) |

**P1/P2 — Robustness & window**
Whitespace-only input trimming (T-050), 500-char input (T-051), Unicode/RTL rendering (T-052), tab order covers all controls (T-060), Enter-in-textbox triggers Search if intended (T-061), maximize/restore leaves all elements resolvable (T-070), Close button exits cleanly (T-071).

## Example runnable plan (T-030)

```json
{
  "schemaVersion": "1.0",
  "testId": "T-030-add-valid-patient",
  "applicationKey": "patient-demo",
  "testData": { "patientId": "P900001", "first": "Ada", "last": "Lovelace", "dob": "1990-04-17" },
  "steps": [
    { "stepNumber": 1, "action": "waitFor", "target": { "automationId": "StatusMessage" },
      "condition": { "property": "name", "operator": "equals", "expectedValue": "Ready" } },
    { "stepNumber": 2, "action": "assert", "target": { "automationId": "UpdateButton", "controlType": "button" },
      "property": "enabled", "operator": "notEquals", "expectedValue": "True" },
    { "stepNumber": 3, "action": "setText", "target": { "automationId": "PatientIdTextBox", "controlType": "edit" }, "value": "P900001" },
    { "stepNumber": 4, "action": "setText", "target": { "automationId": "FirstNameTextBox", "controlType": "edit" }, "value": "Ada" },
    { "stepNumber": 5, "action": "setText", "target": { "automationId": "LastNameTextBox", "controlType": "edit" }, "value": "Lovelace" },
    { "stepNumber": 6, "action": "setText",
      "target": { "automationId": "PART_TextBox", "ancestorAutomationId": "DateOfBirthDatePicker", "controlType": "edit" },
      "value": "1990-04-17" },
    { "stepNumber": 7, "action": "invoke", "target": { "automationId": "AddButton", "controlType": "button" } },
    { "stepNumber": 8, "action": "waitFor", "target": { "automationId": "StatusMessage" },
      "condition": { "property": "name", "operator": "contains", "expectedValue": "added", "timeoutMilliseconds": 5000 } },
    { "stepNumber": 9, "action": "captureScreenshot", "name": "after-add" }
  ]
}
```

Note step 6: the DatePicker itself isn't an edit control, so text has to go to the nested `PART_TextBox` scoped by `ancestorAutomationId`.

## Blockers you'd need to fix before some of this is automatable

1. **Grid rows and cells have empty AutomationIds.** `select_item` requires an exact `itemAutomationId`, so T-035/036/037 — the entire selection-dependent path — cannot run today. Add `AutomationProperties.AutomationId` to the row container, e.g. `PatientRow_P900001`.
2. **Grid content comes back `[REDACTED]`** from the harness, so you can't assert on names or DOB in the grid even once rows are addressable. Assert against non-PHI surrogates instead — which means adding a `ResultsCountText` element bound to the row count, since `assert_state` has no count operator.
3. **The calendar popup is unaddressable** (`PART_Button` opens it, but nothing inside has an ID). Test dates through typed text and treat the calendar path as manual, or add IDs to the popup.
4. **No confirmation dialog is in the tree.** If Delete raises one, it'll need `ConfirmDeleteDialog` / `ConfirmDeleteYesButton` IDs before T-037 can complete.

If you want, I can run the P0 smoke and validation tests now against the open session and report which pass — the validation ones need no code changes.

# Run the P0 smoke and validation tests now against the open session and report which pass

Starting with the smoke tests (T-001 to T-003).T-001 through T-003 pass. Now the validation suite — T-010, submitting a completely blank form.All four messages fired correctly. But the Add button flipped to disabled on invoke — checking whether it recovers.T-010 passes fully. Now T-011 — isolating a single invalid field to check for over-eager validation.Found a harness limitation — noting it and working around with a direct read.T-011 passes — only the Patient ID message fired, and the grid still holds 3 rows. Now T-012, a malformed ID.T-012 passes with a specific message. Now T-018 — a date of birth in the future.T-018 passes. Now T-021 — garbage typed into the date field.The message looks stale — checking whether the picker's text and bound value have desynced.Confirmed a desync: the visible text is `13/45/2020` but the bound value is still `1/1/2030`. Testing whether that can silently save wrong data.The blank-form path blocks correctly. Testing the more dangerous variant — garbage typed over an already-committed date.Date is now committed as `3/15/1985`. Overwriting the visible text with garbage and completing the form.Confirmed. Capturing evidence.Ran the P0 suite. Seven passes, one real defect — and it's a data-integrity one.

## Results

| ID | Test | Result |
|---|---|---|
| T-001 | Launch, `StatusMessage` = "Ready" | **Pass** |
| T-002 | Search/Add/Clear enabled, Update/Delete disabled | **Pass** |
| T-003 | Editor and all validation messages empty at rest | **Pass** |
| T-010 | Blank form rejected | **Pass** — all four field messages fired, status "Please correct the highlighted fields.", no row added |
| T-011 | Only Patient ID blank | **Pass** — only `PatientIdValidationMessage` populated; no over-eager validation |
| T-012 | Malformed ID `abc!` | **Pass** — "Patient ID may contain only letters, numbers, and hyphens." |
| T-018 | Future DOB `2030-01-01` | **Pass** — "Date of birth cannot be in the future." |
| T-022 | Clear resets validation state | **Pass** |
| T-021 | Garbage in the date field | **FAIL** — see below |

Not run: T-013 (duplicate ID) needs a seeded Patient ID, which is unreadable behind grid redaction. T-015/016/019/020 are still open.

## Defect: DatePicker text desyncs from bound value, saving invisible data

**Severity: high.** The date textbox and `SelectedDate` diverge when text can't be parsed. The textbox keeps the garbage; the bound value silently retains the previous date. Validation then runs against the value the user can't see, and Add succeeds.

Repro:
1. Clear. Enter ID `P900002`, last name `Hopper`, DOB `1985-03-15`, leave first name blank.
2. Add → fails on first name, but the date commits (`DateOfBirthDatePicker.value` = `3/15/1985`).
3. Overwrite the date textbox with `not-a-date`. Fill in first name `Grace`.
4. Add → **"Patient P900002 added."**

The record was written with DOB 1985-03-15 while the screen read `not-a-date`. On a patient record where DOB is an identity field, that's a silent wrong-data write, not a cosmetic issue. I'd fix by blocking submit whenever the picker's text is non-empty and unparseable, rather than falling back to the last good value.

Two related smaller bugs from the same root cause: with an empty picker, unparseable text produces "Date of birth is required." instead of a format error; with a previously committed date, it produces a stale message describing the old invisible value (I saw "cannot be in the future" while the box showed `13/45/2020`).

## Three things that affect how you write the rest of the suite

**Add transiently disables on invoke.** Every `invoke` on `AddButton` returned `isEnabled: false` immediately after, re-enabling within a second. Tests must `waitFor` enabled rather than asserting straight after invoke, or they'll flake.

**`assert_state` can't assert emptiness.** `equals` with `expectedValue: ""` is rejected as a missing parameter, and there's no `empty` operator — so "this validation message is clear" is unassertable. I worked around it with `find_element` and reading `name` manually. Worth requesting an `empty` operator on the harness; otherwise every negative validation assertion needs the workaround.

**`capture_screenshot` timed out** after 4 minutes and never returned, so the defect above has no attached screenshot. That'll break any plan using the `captureScreenshot` step.

One cleanup note: `P900002` (Grace Hopper, DOB 1985-03-15) is now in the dataset, and I can't remove it — deletion needs row selection, which is the unaddressable-grid blocker from earlier. The session is still open if you want to look at it, or I can close it and run the remaining validation cases (T-015/016/019/020) first.