# Test Documentation Guidelines (StyleCop-compatible)

Purpose: Provide concise XML documentation templates for test classes and methods that comply with StyleCop rules while remaining low-maintenance.

General rules:
- Keep `<summary>` short (one sentence) describing *what* the test verifies, not implementation details.
- Use `<remarks>` only for important context or TODOs that need follow-up.
- Avoid duplicating assertions or implementation in the summary — state observable behavior.
- Use passive phrasing sparingly; prefer clear action (`Creates user and asserts persisted`).
- If a test is a placeholder, mark it with an `[Ignore]` attribute and add a `<remarks>` explaining the missing setup.

Class template (example):

```csharp
/// <summary>
/// Tests the `MyService` behaviors for edge cases and integration points.
/// </summary>
[TestClass]
public class MyServiceTests
{
    /// <summary>
    /// Verifies that DoWorkAsync returns expected result when input is valid.
    /// </summary>
    [TestMethod]
    public async Task DoWorkAsync_ValidInput_ReturnsExpected()
    {
        // arrange / act / assert
    }
}
```

Method guidance:
- Begin summary with a verb describing the operation (Verifies/Get/Returns/Sets).
- If multiple asserts are present, summarize the overall behavior (e.g., "Adds claims and verifies count").
- For placeholder tests that require extra setup, include `<remarks>` explaining required dependencies (e.g., token providers).

Example placeholder method:

```csharp
/// <summary>
/// Placeholder: requires a registered PhoneNumberTokenProvider to fully exercise token generation.
/// </summary>
/// <remarks>See Tests/TEST_DOC_REPORT.md for follow-up notes.</remarks>
[TestMethod]
[Ignore("Placeholder - needs token provider")]
public void GenerateChangePhoneNumberTokenAsyncTest() { }
```

Maintenance:
- Keep summaries minimal to reduce drift when implementations change.
- Use `Tests/TEST_DOC_REPORT.md` for per-method analysis and flagged mismatches.

EOF
