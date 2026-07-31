namespace WpfAiAutomation.Contracts;

public enum AutomationAction
{
    SetText,
    Invoke,
    SelectItem,
    ReadElement,
    WaitFor,
    Assert,
    CaptureScreenshot,
}

public enum AutomationControlType
{
    Button,
    CheckBox,
    ComboBox,
    Edit,
    List,
    ListItem,
    Text,
    Window,
}

public enum AutomationProperty
{
    Exists,
    Name,
    Value,
    Enabled,
    Visible,
}

public enum ComparisonOperator
{
    Exists,
    NotExists,
    Equals,
    NotEquals,
    Contains,
    NotEmpty,
    Enabled,
    Visible,
}

public enum EvidenceResult
{
    Passed,
    Failed,
    Skipped,
}

public enum ToolErrorCode
{
    InvalidRequest,
    NoActiveSession,
    ApplicationNotAllowed,
    ApplicationLaunchFailed,
    WindowTimeout,
    ElementNotFound,
    AmbiguousElement,
    ElementDisabled,
    UnsupportedPattern,
    ConditionTimeout,
    AssertionFailed,
    PolicyDenied,
    AutomationFailure,
}
