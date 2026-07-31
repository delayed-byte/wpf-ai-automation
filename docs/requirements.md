The goal is to control WPF ui application via Ai agent to create automation tests. How this can be accomplished

I already have sample Patient Management Demo WPF app with automationIDs added to it.


## Recommended architecture

```text
Test objective / requirements
          ↓
      AI agent
  plans test steps
          ↓
WPF Automation Tool API
  - inspect UI
  - find control
  - click/type/select
  - read properties
  - capture screenshot
  - wait/assert
          ↓
FlaUI using Microsoft UI Automation
          ↓
       WPF application
          ↓
Structured execution log + generated xUnit test
```

Microsoft UI Automation exposes WPF controls as a programmatically accessible element tree and supports automated interaction with those controls. FlaUI is a .NET wrapper over Microsoft UI Automation and supports WPF applications. ([Microsoft Learn][1])

The AI agent should **not control the mouse and keyboard directly**. It should invoke a small set of deterministic automation tools implemented in C#.

## 1. Make the WPF application automation-friendly

Assign stable `AutomationId` values to all important controls:

```xml
<TextBox
    x:Name="PatientIdTextBox"
    AutomationProperties.AutomationId="PatientIdTextBox"
    AutomationProperties.Name="Patient ID" />

<Button
    x:Name="SearchButton"
    AutomationProperties.AutomationId="SearchButton"
    AutomationProperties.Name="Search patient"
    Content="Search" />

<TextBlock
    AutomationProperties.AutomationId="PatientNameLabel"
    Text="{Binding PatientName}" />
```

The agent should locate controls in this order:

1. `AutomationId`
2. Control type plus `AutomationId`
3. Accessible name
4. Parent-child relationship
5. Visible text only as a last resort

Avoid screen coordinates such as:

```csharp
Mouse.Click(new Point(525, 340));
```

Coordinates are fragile because they depend on resolution, scaling, window position, and layout.

For custom WPF controls, implement an appropriate `AutomationPeer` so the control exposes its value, state, and supported operations through UI Automation. WPF represents automation information through a parallel tree of automation peer objects. ([Microsoft Learn][2])

## 2. Build a deterministic WPF automation library

Suggested solution structure:

```text
WpfAiTesting.sln
│
├── WpfAutomation.Core
│   ├── ApplicationController.cs
│   ├── UiInspector.cs
│   ├── UiActions.cs
│   ├── UiAssertions.cs
│   └── ScreenshotService.cs
│
├── WpfAutomation.AgentServer
│   ├── AutomationTools.cs
│   └── Program.cs
│
├── WpfAutomation.Tests
│   ├── Pages
│   └── Tests
│
└── ApplicationUnderTest
```

Install:

```bash
dotnet add package FlaUI.Core
dotnet add package FlaUI.UIA3
dotnet add package xunit
dotnet add package xunit.runner.visualstudio
```

Basic application controller:

```csharp
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;

public sealed class WpfApplicationController : IDisposable
{
    private readonly Application _application;
    private readonly UIA3Automation _automation;

    public WpfApplicationController(string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);

        _application = Application.Launch(executablePath);
        _automation = new UIA3Automation();
    }

    public Window GetMainWindow(TimeSpan timeout)
    {
        Window? window = null;
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            window = _application.GetMainWindow(_automation);

            if (window is not null)
            {
                return window;
            }

            Thread.Sleep(200);
        }

        throw new TimeoutException("The WPF main window did not appear.");
    }

    public void Dispose()
    {
        _application.Close();
        _automation.Dispose();
        _application.Dispose();
    }
}
```

## 3. Expose controlled actions to the AI agent

Give the agent narrowly defined tools such as:

```text
launch_application
close_application
get_ui_tree
find_element
click_element
set_text
select_item
read_element
wait_for_element
assert_element
take_screenshot
```

Example tool contracts:

```csharp
public sealed record FindElementRequest(
    string AutomationId,
    string? ControlType = null,
    int TimeoutMilliseconds = 5000);

public sealed record ElementInformation(
    string AutomationId,
    string Name,
    string ControlType,
    bool IsEnabled,
    bool IsVisible,
    string? Value);

public sealed record AssertionRequest(
    string AutomationId,
    string Property,
    string Operator,
    string ExpectedValue,
    int TimeoutMilliseconds = 5000);
```

An MCP server is one option for exposing these operations. MCP tools have names and structured input schemas that AI models can discover and invoke. ([Model Context Protocol][3])

Example conceptual MCP tools:

```csharp
[McpServerTool]
[Description("Returns the visible UI Automation tree for the WPF application.")]
public UiTreeResult GetUiTree()
{
    return _inspector.GetVisibleTree();
}

[McpServerTool]
[Description("Clicks an enabled UI element identified by its exact AutomationId.")]
public ActionResult ClickElement(string automationId)
{
    return _actions.ClickByAutomationId(automationId);
}

[McpServerTool]
[Description("Sets the text of an editable control identified by AutomationId.")]
public ActionResult SetText(string automationId, string value)
{
    return _actions.SetText(automationId, value);
}
```

MCP is optional. The same design can use:

* a local HTTP API;
* named pipes;
* direct .NET function calling;
* a command-line executable accepting JSON.

The important design decision is the **strict automation boundary**, not the transport protocol.

## 4. Return a simplified UI model to the agent

Do not send the complete raw UI Automation tree on every step. It can be large and noisy.

Return a simplified structure:

```json
{
  "window": {
    "automationId": "MainWindow",
    "title": "Patient Manager"
  },
  "elements": [
    {
      "automationId": "PatientIdTextBox",
      "controlType": "Edit",
      "name": "Patient ID",
      "enabled": true,
      "value": ""
    },
    {
      "automationId": "SearchButton",
      "controlType": "Button",
      "name": "Search patient",
      "enabled": true
    },
    {
      "automationId": "PatientNameLabel",
      "controlType": "Text",
      "name": "",
      "value": ""
    }
  ]
}
```

This lets the agent reason:

```text
1. Set PatientIdTextBox to "P10023".
2. Click SearchButton.
3. Wait for PatientNameLabel to become non-empty.
4. Verify PatientNameLabel equals "John Smith".
```

## 5. Separate planning from execution

Use two distinct phases.

### Planning phase

The agent converts a requirement into structured test steps:

```json
{
  "testName": "SearchForExistingPatient",
  "preconditions": [
    "Application is running",
    "Patient P10023 exists"
  ],
  "steps": [
    {
      "action": "setText",
      "target": "PatientIdTextBox",
      "value": "P10023"
    },
    {
      "action": "click",
      "target": "SearchButton"
    },
    {
      "action": "waitFor",
      "target": "PatientNameLabel",
      "property": "value",
      "operator": "notEmpty",
      "timeoutMilliseconds": 5000
    },
    {
      "action": "assert",
      "target": "PatientNameLabel",
      "property": "value",
      "operator": "equals",
      "expected": "John Smith"
    }
  ]
}
```

### Execution phase

A deterministic runner validates and executes each step.

The runner must reject:

* unknown actions;
* missing controls;
* ambiguous matches;
* coordinate-based operations;
* unsupported properties;
* excessive timeouts;
* application or process access outside the allowlist.

The LLM decides **what should be tested**. Normal C# code decides **exactly how each action is executed**.

## 6. Implement actions with retry and validation

```csharp
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;

public sealed class UiActions
{
    private readonly Window _window;

    public UiActions(Window window)
    {
        _window = window;
    }

    public void SetText(string automationId, string value)
    {
        var element = FindRequiredElement(automationId);
        var textBox = element.AsTextBox();

        if (!textBox.IsEnabled)
        {
            throw new InvalidOperationException(
                $"Control '{automationId}' is disabled.");
        }

        textBox.Enter(value);

        if (!string.Equals(textBox.Text, value, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Control '{automationId}' did not accept the expected value.");
        }
    }

    public void Click(string automationId)
    {
        var element = FindRequiredElement(automationId);

        if (!element.IsEnabled)
        {
            throw new InvalidOperationException(
                $"Control '{automationId}' is disabled.");
        }

        element.Click();
    }

    private AutomationElement FindRequiredElement(string automationId)
    {
        var element = _window.FindFirstDescendant(
            condition => condition.ByAutomationId(automationId));

        return element ??
            throw new InvalidOperationException(
                $"Control '{automationId}' was not found.");
    }
}
```

For production use, replace fixed sleeps with condition-based waits:

```csharp
public string WaitForText(
    string automationId,
    Func<string, bool> condition,
    TimeSpan timeout)
{
    var deadline = DateTime.UtcNow + timeout;

    while (DateTime.UtcNow < deadline)
    {
        var element = FindRequiredElement(automationId);
        var value = element.Properties.Name.ValueOrDefault;

        if (condition(value))
        {
            return value;
        }

        Thread.Sleep(100);
    }

    throw new TimeoutException(
        $"Expected condition was not met for '{automationId}'.");
}
```

## 7. Generate maintainable xUnit tests

The agent can first explore the application and produce a test plan. After review, it generates stable test code:

```csharp
public sealed class PatientSearchTests
{
    [Fact]
    [Trait("Category", "Smoke")]
    public void SearchForExistingPatient_DisplaysPatientName()
    {
        using var app = new WpfApplicationController(
            TestConfiguration.ApplicationPath);

        var window = app.GetMainWindow(TimeSpan.FromSeconds(10));
        var patientPage = new PatientSearchPage(window);

        patientPage.EnterPatientId("P10023");
        patientPage.Search();

        Assert.Equal(
            "John Smith",
            patientPage.WaitForPatientName(TimeSpan.FromSeconds(5)));
    }
}
```

Page object:

```csharp
public sealed class PatientSearchPage
{
    private readonly Window _window;

    public PatientSearchPage(Window window)
    {
        _window = window;
    }

    public void EnterPatientId(string patientId)
    {
        GetById("PatientIdTextBox")
            .AsTextBox()
            .Enter(patientId);
    }

    public void Search()
    {
        GetById("SearchButton").AsButton().Invoke();
    }

    public string WaitForPatientName(TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            var text = GetById("PatientNameLabel").AsLabel().Text;

            if (!string.IsNullOrWhiteSpace(text))
            {
                return text;
            }

            Thread.Sleep(100);
        }

        throw new TimeoutException("Patient name was not displayed.");
    }

    private AutomationElement GetById(string automationId)
    {
        return _window.FindFirstDescendant(
                   condition => condition.ByAutomationId(automationId))
               ?? throw new InvalidOperationException(
                   $"Element '{automationId}' was not found.");
    }
}
```

The generated test code becomes the maintained test asset. The AI agent does not need to improvise the complete test every time it runs.

## 8. Support two operating modes

### Exploration mode

The agent:

1. launches the application;
2. inspects the UI tree;
3. navigates through screens;
4. captures available controls and states;
5. proposes test scenarios;
6. generates test code.

Exploration mode should run only in an isolated test environment.

### Regression mode

CI executes checked-in xUnit/FlaUI tests without allowing the agent to alter their behavior.

```bash
dotnet test \
  --filter "Category=Smoke" \
  --logger "trx;LogFileName=smoke-tests.trx"
```

This separation is especially important for regulated software:

```text
AI exploration and test generation
             ↓
Human technical review
             ↓
Approved/versioned test source
             ↓
Deterministic CI execution
             ↓
Immutable results and evidence
```

## 9. Add an observation-action loop

For exploratory operation, use this loop:

```text
Observe → Plan → Validate → Act → Verify → Record
```

Example:

```text
Observe:
SearchButton is disabled.
PatientIdTextBox is empty.

Plan:
Enter a patient ID.

Validate:
PatientIdTextBox exists, is editable, and is enabled.

Act:
Set text to P10023.

Verify:
PatientIdTextBox value equals P10023.
SearchButton is now enabled.

Record:
Save action, before-state, after-state, timestamp, and screenshot.
```

Every action should have:

* preconditions;
* expected state change;
* timeout;
* postcondition;
* failure evidence.

## 10. Test evidence to capture

For every execution, store:

```text
Test run ID
Application version
Test package commit
Agent/model version, when used
Machine and operating-system information
Test data set/version
Requested action
Resolved AutomationId
Observed state before action
Observed state after action
Assertions
Timestamps
Screenshots on failure
Application logs
Automation logs
Final pass/fail result
```

Example structured event:

```json
{
  "runId": "RUN-2026-07-31-001",
  "testId": "PATIENT-SEARCH-001",
  "step": 2,
  "action": "click",
  "automationId": "SearchButton",
  "precondition": {
    "exists": true,
    "enabled": true
  },
  "result": "passed",
  "durationMilliseconds": 184,
  "timestampUtc": "2026-07-31T15:12:41.381Z"
}
```

## 11. Guardrails

The automation server should enforce:

```text
Only the approved application executable may be launched.
Only approved processes may be inspected.
Only declared automation operations may be invoked.
No arbitrary shell or PowerShell execution.
No arbitrary file-system access.
No coordinate clicks unless explicitly approved.
No destructive actions without an additional authorization rule.
Every call must be logged.
Tool arguments must be schema-validated.
Execution must have global and per-action timeouts.
```

For sensitive operations, classify actions:

```text
Read-only:
inspect, read, screenshot

Normal:
click, type, select, navigate

Restricted:
delete, submit, reset, install, firmware update

Prohibited:
shell execution, registry modification, uncontrolled process launch
```

## Recommended implementation sequence

1. Add stable `AutomationId` values to the WPF application.
2. Use FlaUInspect or Microsoft accessibility inspection tools to verify the UI Automation tree.
3. Build a FlaUI-based C# automation library.
4. Implement approximately ten narrow agent tools.
5. Return a simplified UI tree as structured JSON.
6. Build the observe-plan-validate-act-verify loop.
7. Record screenshots, logs, actions, and assertions.
8. Generate Page Objects and xUnit tests from successful exploratory sessions.
9. Require review before generated tests enter the regression suite.
10. Execute approved tests deterministically through GitHub Actions on an interactive Windows runner.

The central design principle is:

```text
AI agent = test planner and code generator
FlaUI = WPF interaction layer
C# runner = validation and deterministic execution
xUnit = maintained regression tests
CI/CD = controlled execution and evidence collection
```

[1]: https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-uiautomationoverview?utm_source=chatgpt.com "UI Automation Overview - Win32 apps"
[2]: https://learn.microsoft.com/en-us/dotnet/desktop/wpf/controls/ui-automation-of-a-wpf-custom-control?utm_source=chatgpt.com "UI Automation of a WPF Custom Control"
[3]: https://modelcontextprotocol.io/specification/2026-07-28/server/tools?utm_source=chatgpt.com "Tools"
