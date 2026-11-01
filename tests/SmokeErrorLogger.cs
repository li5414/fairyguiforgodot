// FairyGUI for Godot - smoke test helper (not a port of a Unity file).
// Counts engine errors (GD.PushError, failed ERR_* checks, unhandled C# exceptions in engine callbacks) while the
// smoke test runs, so failures that never reach a try/catch in SmokeTest still fail the run.
using System.Collections.Generic;
using System.Threading;
using Godot;

public partial class SmokeErrorLogger : Logger
{
    const int MaxMessages = 50;

    readonly object _lock = new object();
    readonly List<string> _messages = new List<string>();
    int _errorCount;
    int _warningCount;

    /// <summary>
    /// Only counts while true (set by SmokeTest; off during shutdown).
    /// </summary>
    public volatile bool active;

    /// <summary>
    /// What the test is doing right now (phase / package / component), attached to each recorded error.
    /// </summary>
    public volatile string context = "";

    public int errorCount
    {
        get { return Volatile.Read(ref _errorCount); }
    }

    public int warningCount
    {
        get { return Volatile.Read(ref _warningCount); }
    }

    // Called from any thread (Godot docs): only lock-protected bookkeeping here, never print from this method.
    public override void _LogError(string function, string file, int line, string code, string rationale,
        bool editorNotify, int errorType, Godot.Collections.Array<ScriptBacktrace> scriptBacktraces)
    {
        if (!active)
            return;

        if (errorType == (int)ErrorType.Warning)
        {
            Interlocked.Increment(ref _warningCount);
            return;
        }

        Interlocked.Increment(ref _errorCount);

        string text = string.IsNullOrEmpty(rationale) ? code : rationale;
        string msg = "[" + context + "] " + text + "  (" + file + ":" + line + " " + function + ")";
        lock (_lock)
        {
            if (_messages.Count < MaxMessages)
                _messages.Add(msg);
        }
    }

    public override void _LogMessage(string message, bool error)
    {
        // plain prints (GD.Print / GD.PrintErr) are not errors
    }

    public List<string> GetMessages()
    {
        lock (_lock)
        {
            return new List<string>(_messages);
        }
    }
}
