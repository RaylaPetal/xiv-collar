namespace Oathbound.Plugin.Commands;

/// Shared outcome type for `ChatCommandListener.Resolve`/`TestIncomingCommand` (the dispatch path both a
/// real incoming tell and Settings' "Test an Owner command" control go through) - previously also used by
/// the now-removed per-action `LocalTestCoordinator`, kept here as its own file since that class is gone.
public readonly record struct LocalTestResult(bool Success, string Message)
{
    public static LocalTestResult Ok(string message) => new(true, message);
    public static LocalTestResult Fail(string message) => new(false, message);
}
