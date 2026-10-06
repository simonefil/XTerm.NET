namespace XTerm.Tests;

/// <summary>
///     ANSI escape definitions and utility methods, for tests that drive a terminal the way a
///     program does.
/// </summary>
/// <remarks>
/// <para>Shaped after <c>Consolonia.Core.Text.Esc</c>: named constants for whole sequences and
/// builders for the ones that take arguments, with the coordinates zero-based on the way in.</para>
/// <para>Here rather than in each test class because the escape character had been written out in
/// twenty files, several of them with a private <c>Apc</c> beside it. A constant repeated per file
/// is one that can be got wrong per file, and a mistyped escape gives a test that passes while
/// driving the terminal with something other than what it claims to send.</para>
/// <para>In the test project rather than in the emulator because these are sequences to SEND, and
/// XTerm.NET's job is to receive them. Worth promoting to the library if consumers ever want them.
/// </para>
/// </remarks>
public static class Esc
{
    /// <summary>The string terminator that closes a DCS or APC sequence.</summary>
    public const string St = "\u001b\\";

    // screen buffer
    public const string ClearScreen = "\u001b[2J";
    public const string ClearLine = "\u001b[2K";

    // mouse reporting (DECSET)
    public const string EnableMouseTracking = "\u001b[?1000h";
    public const string EnableSgrMouse = "\u001b[?1006h";
    public const string EnableSgrPixelsMouse = "\u001b[?1016h";

    /// <summary>A control sequence: ESC [ followed by the body.</summary>
    public static string Csi(string body)
    {
        return $"\u001b[{body}";
    }

    /// <summary>
    ///     Moves the cursor, taking the zero-based coordinates the buffer uses.
    /// </summary>
    /// <remarks>
    ///     CUP is one-based on the wire and the buffer is zero-based, so the conversion belongs
    ///     somewhere it can only be written once -- an off-by-one in a cursor address is the classic
    ///     way for a graphics test to assert against the wrong cell.
    /// </remarks>
    public static string SetCursorPosition(int x, int y)
    {
        return $"\u001b[{y + 1};{x + 1}H";
    }

    /// <summary>
    ///     An SGR mouse report (1006, or 1016 in pixels), taking a zero-based cell or pixel.
    /// </summary>
    /// <remarks>
    ///     One-based on the wire like CUP, and converted here for the same reason. The terminator
    ///     is the only thing telling a press from a release, so it comes from a flag rather than a
    ///     hand-typed M or m.
    /// </remarks>
    public static string SgrMouseReport(int button, int x, int y, bool press = true)
    {
        return $"\u001b[<{button};{x + 1};{y + 1}{(press ? 'M' : 'm')}";
    }

    /// <summary>
    ///     An application programming command, which is how the Kitty graphics protocol arrives.
    /// </summary>
    /// <remarks>
    ///     The payload is separated by a semicolon and omitted entirely when there is none: a
    ///     trailing semicolon with nothing after it is a different sequence from no payload at all.
    /// </remarks>
    public static string Apc(string control, string payload = "")
    {
        return payload.Length == 0
            ? $"\u001b_G{control}{St}"
            : $"\u001b_G{control};{payload}{St}";
    }
}
