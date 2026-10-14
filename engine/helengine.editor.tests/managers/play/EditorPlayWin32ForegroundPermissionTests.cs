using System.Runtime.ExceptionServices;
using Xunit;

namespace helengine.editor.tests.managers.play;

/// <summary>
/// Verifies the Win32 foreground grant reports its outcome through return values, never through exceptions, whether or
/// not the test host is allowed to set the foreground.
/// </summary>
public sealed class EditorPlayWin32ForegroundPermissionTests {
    /// <summary>
    /// Ensures a grant for the test host's own process raises no first-chance exception and reports a consistent
    /// outcome: success with error code 0, or refusal with a non-zero Win32 error code.
    /// </summary>
    [Fact]
    public void TryAllow_ReportsOutcomeWithoutExceptions() {
        EditorPlayWin32ForegroundPermission permission = new EditorPlayWin32ForegroundPermission();
        int firstChanceExceptionCount = 0;
        int testThreadId = Environment.CurrentManagedThreadId;
        EventHandler<FirstChanceExceptionEventArgs> handler = (sender, args) => {
            if (Environment.CurrentManagedThreadId == testThreadId) {
                Interlocked.Increment(ref firstChanceExceptionCount);
            }
        };

        bool allowed;
        int errorCode;
        AppDomain.CurrentDomain.FirstChanceException += handler;
        try {
            allowed = permission.TryAllow(Environment.ProcessId, out errorCode);
        } finally {
            AppDomain.CurrentDomain.FirstChanceException -= handler;
        }

        Assert.Equal(0, firstChanceExceptionCount);
        Assert.Equal(allowed, errorCode == 0);
    }
}
