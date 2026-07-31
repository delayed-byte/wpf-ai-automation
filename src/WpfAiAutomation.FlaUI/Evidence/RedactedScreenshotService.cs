using System.Drawing;
using System.Drawing.Imaging;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Exceptions;
using WpfAiAutomation.Contracts;

namespace WpfAiAutomation.FlaUI;

public interface IScreenshotArtifactWriter
{
    void Save(Bitmap image, string path);
}

public sealed class RedactedScreenshotService
{
    private readonly EvidencePathConfiguration _configuration;
    private readonly SensitiveDataRedactor _redactor;
    private readonly IScreenshotArtifactWriter _writer;
    private readonly DeterministicAutomationFaultInjector _faultInjector;

    public RedactedScreenshotService(
        EvidencePathConfiguration configuration,
        SensitiveDataRedactor redactor,
        IScreenshotArtifactWriter? writer = null,
        DeterministicAutomationFaultInjector? faultInjector = null)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _redactor = redactor ?? throw new ArgumentNullException(nameof(redactor));
        _writer = writer ?? new PngScreenshotArtifactWriter();
        _faultInjector = faultInjector ?? new DeterministicAutomationFaultInjector();
    }

    public Task<ToolResult<EvidenceReference>> CaptureAsync(
        AutomationSessionManager sessionManager,
        string sessionId,
        AutomationOperationContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sessionManager);
        ArgumentNullException.ThrowIfNull(context);
        ValidateContext(context);

        return sessionManager.ExecuteSerializedAsync(session =>
        {
            if (!string.Equals(session.SessionId, sessionId, StringComparison.Ordinal))
            {
                throw new AutomationOperationException(ToolErrorCode.NoActiveSession, "The request does not reference the active session.");
            }

            var relativePath = Path.Combine("screenshots", $"step-{context.StepNumber:D4}-{context.CorrelationId}.png");
            var path = Path.Combine(Path.GetFullPath(_configuration.RootDirectory), context.RunId, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            _faultInjector.ThrowIfConfigured(AutomationFaultPoint.CaptureScreenshot);
            using var image = session.MainWindow.Capture();
            Redact(image, session.MainWindow);
            _writer.Save(image, path);
            return new EvidenceReference(relativePath.Replace(Path.DirectorySeparatorChar, '/'), "screenshot");
        }, cancellationToken);
    }

    private void Redact(Bitmap image, AutomationElement root)
    {
        using var graphics = Graphics.FromImage(image);
        if (root.FindAllDescendants().Append(root).Any(IsSensitive))
        {
            // UIA bounds and capture pixels can use different DPI coordinate spaces.
            // A full-frame mask is intentionally conservative: an evidence screenshot
            // must never expose a configured sensitive control while preserving a
            // screenshot artifact and its correlation to the failing step.
            graphics.FillRectangle(Brushes.Black, new Rectangle(Point.Empty, image.Size));
            return;
        }

        var rootBounds = root.BoundingRectangle;
        foreach (var element in root.FindAllDescendants().Append(root))
        {
            if (!TryGetSensitiveBounds(element, rootBounds, image.Size, out var redactionBounds))
            {
                continue;
            }

            if (redactionBounds.Width > 0 && redactionBounds.Height > 0)
            {
                graphics.FillRectangle(Brushes.Black, redactionBounds);
            }
        }
    }

    private bool IsSensitive(AutomationElement element)
    {
        try
        {
            return _redactor.IsSensitive(element.AutomationId);
        }
        catch (PropertyNotSupportedException)
        {
            return false;
        }
        catch (ElementNotAvailableException)
        {
            return false;
        }
    }

    private bool TryGetSensitiveBounds(AutomationElement element, Rectangle rootBounds, Size imageSize, out Rectangle redactionBounds)
    {
        try
        {
            if (!_redactor.IsSensitive(element.AutomationId))
            {
                redactionBounds = Rectangle.Empty;
                return false;
            }

            var bounds = element.BoundingRectangle;
            redactionBounds = Rectangle.Intersect(
                new Rectangle(bounds.X - rootBounds.X, bounds.Y - rootBounds.Y, bounds.Width, bounds.Height),
                new Rectangle(Point.Empty, imageSize));
            return true;
        }
        catch (PropertyNotSupportedException)
        {
            redactionBounds = Rectangle.Empty;
            return false;
        }
        catch (ElementNotAvailableException)
        {
            redactionBounds = Rectangle.Empty;
            return false;
        }
    }

    private static void ValidateContext(AutomationOperationContext context)
    {
        if (context.StepNumber < 1
            || !IsSafeIdentifier(context.RunId)
            || !IsSafeIdentifier(context.TestId)
            || !IsSafeIdentifier(context.CorrelationId))
        {
            throw new ArgumentException("Screenshot operation identifiers are invalid.", nameof(context));
        }
    }

    private static bool IsSafeIdentifier(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.All(character => char.IsLetterOrDigit(character) || character is '-' or '_');

    private sealed class PngScreenshotArtifactWriter : IScreenshotArtifactWriter
    {
        public void Save(Bitmap image, string path) => image.Save(path, ImageFormat.Png);
    }
}
