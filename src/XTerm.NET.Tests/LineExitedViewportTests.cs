using XTerm;
using XTerm.Buffer;
using XTerm.Common;
using XTerm.Events;
using XTerm.Options;

namespace XTerm.Tests;

public class LineExitedViewportTests
{
    [Fact]
    public void FullScreenScroll_RaisesBeforeAlternateBufferLineIsRecycled()
    {
        var terminal = new Terminal(new TerminalOptions { Rows = 2, Cols = 8, Scrollback = 1 });
        terminal.SwitchToAltBuffer();
        SetCell(terminal.Buffer.Lines[0]!, "old");

        string? captured = null;
        BufferType? buffer = null;
        LineExitReason? reason = null;
        terminal.LineExitedViewport += (_, args) =>
        {
            captured = args.Line.TranslateToString(trimRight: true);
            buffer = args.Buffer;
            reason = args.Reason;
        };

        terminal.Buffer.ScrollUp(1);

        Assert.Equal("old", captured);
        Assert.Equal(BufferType.Alternate, buffer);
        Assert.Equal(LineExitReason.Scrolled, reason);
    }

    [Fact]
    public void PartialScrollRegion_RaisesForTheLineRemovedFromTheRegion()
    {
        var terminal = new Terminal(new TerminalOptions { Rows = 4, Cols = 8 });
        terminal.Buffer.SetScrollRegion(1, 2);
        SetCell(terminal.Buffer.Lines[1]!, "gone");

        string? captured = null;
        terminal.LineExitedViewport += (_, args) => captured = args.Line.TranslateToString(trimRight: true);

        terminal.Buffer.ScrollUp(1);

        Assert.Equal("gone", captured);
    }

    [Fact]
    public void NarrowedMargins_DoNotRaiseBecauseNoWholeLineLeaves()
    {
        var terminal = new Terminal(new TerminalOptions { Rows = 3, Cols = 8 });
        terminal.Buffer.SetLeftRightMargins(1, 6);
        var count = 0;
        terminal.LineExitedViewport += (_, _) => count++;

        terminal.Buffer.ScrollUp(1);

        Assert.Equal(0, count);
    }

    [Fact]
    public void BufferSwitch_RaisesMeaningfulRowsBeforeBufferChanged()
    {
        var terminal = new Terminal(new TerminalOptions { Rows = 3, Cols = 8 });
        SetCell(terminal.Buffer.Lines[0]!, "first");
        var events = new List<string>();
        TerminalEvents.LineExitedViewportEventArgs? exited = null;
        terminal.LineExitedViewport += (_, args) =>
        {
            exited = args;
            events.Add("exit");
        };
        terminal.BufferChanged += (_, _) => events.Add("changed");

        terminal.SwitchToAltBuffer();

        Assert.NotNull(exited);
        Assert.Equal("first", exited.Line.TranslateToString(trimRight: true));
        Assert.Equal(BufferType.Normal, exited.Buffer);
        Assert.Equal(LineExitReason.BufferDeactivated, exited.Reason);
        Assert.Equal(["exit", "changed"], events);
    }

    private static void SetCell(BufferLine line, string content)
    {
        var cell = new BufferCell(content, 1, AttributeData.Default);
        line.SetCell(0, ref cell);
    }
}
