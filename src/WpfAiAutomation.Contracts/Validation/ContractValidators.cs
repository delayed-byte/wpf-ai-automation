namespace WpfAiAutomation.Contracts;

public static class ContractValidators
{
    public static ValidationResult Validate(ElementLocator? locator, PolicyCeilings? ceilings = null)
    {
        var policy = ceilings ?? PolicyCeilings.Default;
        var errors = new List<ValidationError>();

        if (locator is null)
        {
            errors.Add(Error("locator", "A locator is required."));
            return new ValidationResult(errors);
        }

        if (string.IsNullOrWhiteSpace(locator.AutomationId))
        {
            errors.Add(Error("automationId", "AutomationId is required."));
        }
        else if (locator.AutomationId.Length > policy.MaximumStringLength)
        {
            errors.Add(Error("automationId", "AutomationId exceeds the configured maximum length."));
        }

        if (locator.AncestorAutomationId?.Length > policy.MaximumStringLength)
        {
            errors.Add(Error("ancestorAutomationId", "AncestorAutomationId exceeds the configured maximum length."));
        }

        if (locator.ControlType is { } controlType && !Enum.IsDefined(controlType))
        {
            errors.Add(Error("controlType", "ControlType is not supported."));
        }

        ValidateTimeout(locator.TimeoutMilliseconds, policy, "timeoutMilliseconds", errors);
        return errors.Count == 0 ? ValidationResult.Success : new ValidationResult(errors);
    }

    public static ValidationResult Validate(TestPlan? plan, PolicyCeilings? ceilings = null)
    {
        var policy = ceilings ?? PolicyCeilings.Default;
        var errors = new List<ValidationError>();

        if (plan is null)
        {
            errors.Add(Error("plan", "A test plan is required."));
            return new ValidationResult(errors);
        }

        if (!string.Equals(plan.SchemaVersion, "1.0", StringComparison.Ordinal))
        {
            errors.Add(Error("schemaVersion", "Only schema version 1.0 is supported."));
        }

        ValidateRequired(plan.TestId, "testId", policy, errors);
        ValidateRequired(plan.ApplicationKey, "applicationKey", policy, errors);

        if (plan.Steps is null || plan.Steps.Count == 0)
        {
            errors.Add(Error("steps", "At least one step is required."));
        }
        else if (plan.Steps.Count > policy.MaximumStepCount)
        {
            errors.Add(Error("steps", "The step count exceeds the configured maximum."));
        }

        ValidateTestData(plan.TestData, policy, errors);
        ValidateSteps(plan.Steps, policy, errors);
        return errors.Count == 0 ? ValidationResult.Success : new ValidationResult(errors);
    }

    public static ValidationResult Validate(EvidenceEvent? evidence, PolicyCeilings? ceilings = null)
    {
        var policy = ceilings ?? PolicyCeilings.Default;
        var errors = new List<ValidationError>();

        if (evidence is null)
        {
            errors.Add(Error("evidence", "An evidence event is required."));
            return new ValidationResult(errors);
        }

        ValidateRequired(evidence.RunId, "runId", policy, errors);
        ValidateRequired(evidence.TestId, "testId", policy, errors);
        ValidateRequired(evidence.CorrelationId, "correlationId", policy, errors);

        if (evidence.StepNumber < 1)
        {
            errors.Add(Error("stepNumber", "StepNumber must be greater than zero."));
        }

        if (!Enum.IsDefined(evidence.Action))
        {
            errors.Add(Error("action", "Action is not supported."));
        }

        if (!Enum.IsDefined(evidence.Result))
        {
            errors.Add(Error("result", "Result is not supported."));
        }

        if (evidence.ErrorCode is { } errorCode && !Enum.IsDefined(errorCode))
        {
            errors.Add(Error("errorCode", "ErrorCode is not supported."));
        }

        if (evidence.DurationMilliseconds < 0)
        {
            errors.Add(Error("durationMilliseconds", "DurationMilliseconds cannot be negative."));
        }

        if (evidence.CompletedAtUtc < evidence.StartedAtUtc)
        {
            errors.Add(Error("completedAtUtc", "CompletedAtUtc cannot precede StartedAtUtc."));
        }

        ValidateEvidenceReferences(evidence.Artifacts, policy, errors);
        return errors.Count == 0 ? ValidationResult.Success : new ValidationResult(errors);
    }

    private static void ValidateTestData(
        IReadOnlyDictionary<string, string>? testData,
        PolicyCeilings policy,
        List<ValidationError> errors)
    {
        if (testData is null)
        {
            return;
        }

        foreach (var pair in testData)
        {
            if (!IsValidVariableName(pair.Key))
            {
                errors.Add(Error("testData", "Test data keys must be simple identifiers."));
            }

            if (pair.Value.Length > policy.MaximumStringLength)
            {
                errors.Add(Error("testData", "A test data value exceeds the configured maximum length."));
            }
        }
    }

    private static void ValidateSteps(
        IReadOnlyList<TestStep>? steps,
        PolicyCeilings policy,
        List<ValidationError> errors)
    {
        if (steps is null)
        {
            return;
        }

        var stepNumbers = new HashSet<int>();
        var totalTimeout = 0L;

        foreach (var step in steps)
        {
            if (step is null)
            {
                errors.Add(Error("steps", "A step cannot be null."));
                continue;
            }

            if (step.StepNumber < 1 || !stepNumbers.Add(step.StepNumber))
            {
                errors.Add(Error("steps", "Step numbers must be unique positive integers."));
            }

            totalTimeout += ValidateStep(step, policy, errors);
        }

        if (totalTimeout > policy.MaximumTotalTimeoutMilliseconds)
        {
            errors.Add(Error("steps", "The combined step timeout exceeds the configured maximum."));
        }
    }

    private static int ValidateStep(TestStep step, PolicyCeilings policy, List<ValidationError> errors)
    {
        return step switch
        {
            SetTextStep setText => ValidateTargetAndTimeout(setText.Target, setText.TimeoutMilliseconds, setText.Value, policy, errors),
            InvokeStep invoke => ValidateTargetAndTimeout(invoke.Target, invoke.TimeoutMilliseconds, null, policy, errors),
            SelectItemStep selectItem => ValidateSelectItem(selectItem, policy, errors),
            ReadElementStep read => ValidateRead(read, policy, errors),
            WaitForStep wait => ValidateWait(wait, policy, errors),
            AssertStep assertion => ValidateAssertion(assertion, policy, errors),
            CaptureScreenshotStep screenshot => ValidateScreenshot(screenshot, policy, errors),
            _ => AddUnknownStepError(errors),
        };
    }

    private static int ValidateTargetAndTimeout(
        ElementLocator target,
        int timeoutMilliseconds,
        string? value,
        PolicyCeilings policy,
        List<ValidationError> errors)
    {
        AddErrors(Validate(target, policy), errors);
        ValidateTimeout(timeoutMilliseconds, policy, "timeoutMilliseconds", errors);
        ValidateOptionalLength(value, "value", policy, errors);
        return timeoutMilliseconds;
    }

    private static int ValidateRead(ReadElementStep step, PolicyCeilings policy, List<ValidationError> errors)
    {
        var timeout = ValidateTargetAndTimeout(step.Target, step.TimeoutMilliseconds, null, policy, errors);
        if (!Enum.IsDefined(step.Property))
        {
            errors.Add(Error("property", "Property is not supported."));
        }

        return timeout;
    }

    private static int ValidateSelectItem(SelectItemStep step, PolicyCeilings policy, List<ValidationError> errors)
    {
        var timeout = ValidateTargetAndTimeout(step.Target, step.TimeoutMilliseconds, step.ItemAutomationId, policy, errors);
        ValidateRequired(step.ItemAutomationId, "itemAutomationId", policy, errors);
        return timeout;
    }

    private static int ValidateWait(WaitForStep step, PolicyCeilings policy, List<ValidationError> errors)
    {
        AddErrors(Validate(step.Target, policy), errors);
        ValidateCondition(step.Condition, policy, errors);
        return step.Condition?.TimeoutMilliseconds ?? 0;
    }

    private static int ValidateAssertion(AssertStep step, PolicyCeilings policy, List<ValidationError> errors)
    {
        var timeout = ValidateTargetAndTimeout(step.Target, step.TimeoutMilliseconds, step.ExpectedValue, policy, errors);
        ValidatePropertyAndOperator(step.Property, step.Operator, step.ExpectedValue, errors);
        return timeout;
    }

    private static int ValidateScreenshot(CaptureScreenshotStep step, PolicyCeilings policy, List<ValidationError> errors)
    {
        ValidateRequired(step.Name, "name", policy, errors);
        return 0;
    }

    private static void ValidateCondition(WaitCondition? condition, PolicyCeilings policy, List<ValidationError> errors)
    {
        if (condition is null)
        {
            errors.Add(Error("condition", "A wait condition is required."));
            return;
        }

        ValidatePropertyAndOperator(condition.Property, condition.Operator, condition.ExpectedValue, errors);
        ValidateTimeout(condition.TimeoutMilliseconds, policy, "condition.timeoutMilliseconds", errors);

        if (condition.PollIntervalMilliseconds is < 25 or > 5_000)
        {
            errors.Add(Error("condition.pollIntervalMilliseconds", "PollIntervalMilliseconds must be between 25 and 5000."));
        }

        ValidateOptionalLength(condition.ExpectedValue, "condition.expectedValue", policy, errors);
    }

    private static void ValidatePropertyAndOperator(
        AutomationProperty property,
        ComparisonOperator @operator,
        string? expectedValue,
        List<ValidationError> errors)
    {
        if (!Enum.IsDefined(property))
        {
            errors.Add(Error("property", "Property is not supported."));
        }

        if (!Enum.IsDefined(@operator))
        {
            errors.Add(Error("operator", "Operator is not supported."));
            return;
        }

        var valid = @operator switch
        {
            ComparisonOperator.Exists or ComparisonOperator.NotExists => property == AutomationProperty.Exists,
            ComparisonOperator.NotEmpty => property is AutomationProperty.Name or AutomationProperty.Value,
            ComparisonOperator.Enabled => property == AutomationProperty.Enabled,
            ComparisonOperator.Visible => property == AutomationProperty.Visible,
            ComparisonOperator.Equals or ComparisonOperator.NotEquals or ComparisonOperator.Contains => property != AutomationProperty.Exists,
            _ => false,
        };

        if (!valid)
        {
            errors.Add(Error("operator", "The property and operator combination is not supported."));
        }

        if (@operator is ComparisonOperator.Equals or ComparisonOperator.NotEquals or ComparisonOperator.Contains && string.IsNullOrWhiteSpace(expectedValue))
        {
            errors.Add(Error("expectedValue", "ExpectedValue is required for the selected operator."));
        }
    }

    private static void ValidateEvidenceReferences(
        IReadOnlyList<EvidenceReference>? references,
        PolicyCeilings policy,
        List<ValidationError> errors)
    {
        if (references is null)
        {
            return;
        }

        foreach (var reference in references)
        {
            if (reference is null || string.IsNullOrWhiteSpace(reference.RelativePath) || Path.IsPathRooted(reference.RelativePath) || reference.RelativePath.Contains("..", StringComparison.Ordinal))
            {
                errors.Add(Error("artifacts", "Artifact paths must be safe relative paths."));
                continue;
            }

            ValidateOptionalLength(reference.RelativePath, "artifacts.relativePath", policy, errors);
            ValidateRequired(reference.Kind, "artifacts.kind", policy, errors);
        }
    }

    private static void ValidateTimeout(int value, PolicyCeilings policy, string field, List<ValidationError> errors)
    {
        if (value is < 1 or > 60_000 || value > policy.MaximumTimeoutMilliseconds)
        {
            errors.Add(Error(field, "Timeout must be positive and within the configured maximum."));
        }
    }

    private static void ValidateRequired(string? value, string field, PolicyCeilings policy, List<ValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(Error(field, $"{field} is required."));
        }
        else if (value.Length > policy.MaximumStringLength)
        {
            errors.Add(Error(field, $"{field} exceeds the configured maximum length."));
        }
    }

    private static void ValidateOptionalLength(string? value, string field, PolicyCeilings policy, List<ValidationError> errors)
    {
        if (value?.Length > policy.MaximumStringLength)
        {
            errors.Add(Error(field, $"{field} exceeds the configured maximum length."));
        }
    }

    private static void AddErrors(ValidationResult result, List<ValidationError> errors)
    {
        foreach (var error in result.Errors)
        {
            errors.Add(error);
        }
    }

    private static int AddUnknownStepError(List<ValidationError> errors)
    {
        errors.Add(Error("steps", "The step type is not supported."));
        return 0;
    }

    private static bool IsValidVariableName(string value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && (char.IsLetter(value[0]) || value[0] == '_')
            && value.All(character => char.IsLetterOrDigit(character) || character == '_');
    }

    private static ValidationError Error(string field, string message) => new(field, message);
}
