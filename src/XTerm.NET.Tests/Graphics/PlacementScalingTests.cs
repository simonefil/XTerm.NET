using System.Linq;
using XTerm.Graphics;
using XTerm.Options;

namespace XTerm.Tests.Graphics;

/// <summary>
/// The scaling context a renderer needs to draw a placement's strips: <see
/// cref="LinePlacement.PxPerCellX"/> and <see cref="LinePlacement.PxPerCellY"/>.
///
/// <para>The strips a placement becomes are sliced to whole source pixels, and a renderer that
/// converts them back to screen size has to know how many source pixels one cell of THIS placement
/// covers. Without that it can only assume the image's natural metric -- which drew every stretched
/// picture at its own size instead: striped when blown up, clipped when shrunk. Tom's #114.</para>
/// </summary>
public class PlacementScalingTests
{
    private const string Esc = "";
    private const string St = Esc + "\\";

    private static Terminal Fresh()
        => new(new TerminalOptions
        {
            Cols = 30,
            Rows = 12,
            CellWidthPixels = 2,
            CellHeightPixels = 3
        });

    private static string Apc(string control, string payload = "")
        => payload.Length == 0 ? $"{Esc}_G{control}{St}" : $"{Esc}_G{control};{payload}{St}";

    private static string SolidRgba(int width, int height)
    {
        var bytes = new byte[width * height * 4];
        for (int i = 0; i < bytes.Length; i += 4)
            bytes[i + 3] = 255;
        return Convert.ToBase64String(bytes);
    }

    private static LinePlacement FirstPlacement(Terminal terminal, int screenRow)
        => terminal.Buffer.Lines[terminal.Buffer.YBase + screenRow]!.Placements.First();

    [Fact]
    public void A_stretched_placement_carries_its_boxs_pixels_per_cell()
    {
        // An 8x9 picture stretched into a 2x3 cell box: each cell covers 4x3 source pixels,
        // whatever the image's natural metric says.
        var terminal = Fresh();
        terminal.Write(Apc("a=t,i=1,f=32,s=8,v=9,q=2", SolidRgba(8, 9)));
        terminal.Write(Apc("a=p,i=1,c=2,r=3,q=2"));

        var strip = FirstPlacement(terminal, 0);

        Assert.Equal(8f / 2, strip.PxPerCellX);
        Assert.Equal(9f / 3, strip.PxPerCellY);
    }

    [Fact]
    public void A_natural_placement_carries_zero_meaning_the_images_own_metric()
    {
        // Zero rather than the cell metric itself, so a renderer can tell "natural" apart from
        // "stretched to exactly the natural size" -- the first keeps unstretched edges, and the
        // convention also keeps placements written before the field existed drawing correctly.
        var terminal = Fresh();
        terminal.Write(Apc("a=t,i=1,f=32,s=8,v=9,q=2", SolidRgba(8, 9)));
        terminal.Write(Apc("a=p,i=1,q=2"));

        var strip = FirstPlacement(terminal, 0);

        Assert.Equal(0f, strip.PxPerCellX);
        Assert.Equal(0f, strip.PxPerCellY);
    }

    [Fact]
    public void A_one_pixel_image_stretched_over_a_box_covers_every_row()
    {
        // The overlay idiom: one translucent pixel stretched over a rectangle of cells to tint it.
        // Each cell gets a fraction of a source pixel, which sliced to nothing and dropped the
        // placement entirely.
        var terminal = Fresh();
        terminal.Write(Apc("a=t,i=1,f=32,s=1,v=1,q=2", SolidRgba(1, 1)));
        terminal.Write(Apc("a=p,i=1,c=2,r=3,q=2"));

        for (int row = 0; row < 3; row++)
        {
            var strip = FirstPlacement(terminal, row);
            Assert.Equal(0, strip.Column);
            Assert.Equal(2, strip.Cols);
            Assert.Equal(0, strip.SrcY);
            Assert.Equal(1, strip.SrcHeight);
        }

        Assert.Empty(terminal.Buffer.Lines[terminal.Buffer.YBase + 3]!.Placements);
    }

    [Fact]
    public void An_image_stretched_up_gives_every_row_a_source_pixel()
    {
        // 2x2 over 5x5 cells of 3 pixel rows: 2 source rows over a 15 pixel box. Rows 0, 1 and 3
        // fall inside a single source row and take the one under their centre; rows 2 and 4 span
        // a whole pixel and keep the floor, as stretched strips always have.
        var terminal = Fresh();
        terminal.Write(Apc("a=t,i=1,f=32,s=2,v=2,q=2", SolidRgba(2, 2)));
        terminal.Write(Apc("a=p,i=1,c=5,r=5,q=2"));

        var sourceRows = Enumerable.Range(0, 5).Select(row => FirstPlacement(terminal, row).SrcY).ToArray();

        Assert.Equal(new[] { 0, 0, 0, 1, 1 }, sourceRows);
        Assert.All(Enumerable.Range(0, 5), row => Assert.Equal(1, FirstPlacement(terminal, row).SrcHeight));
    }

    [Fact]
    public void An_image_stretched_down_still_slices_whole_pixels_per_row()
    {
        // The sub-pixel rule only applies when a cell would get no pixels at all; shrinking keeps
        // the proportional slices it always had.
        var terminal = Fresh();
        terminal.Write(Apc("a=t,i=1,f=32,s=8,v=9,q=2", SolidRgba(8, 9)));
        terminal.Write(Apc("a=p,i=1,c=2,r=3,q=2"));

        for (int row = 0; row < 3; row++)
        {
            var strip = FirstPlacement(terminal, row);
            Assert.Equal(row * 3, strip.SrcY);
            Assert.Equal(3, strip.SrcHeight);
        }
    }

    [Fact]
    public void A_sub_pixel_tile_reports_the_source_pixel_under_its_centre()
    {
        // Three source pixels over four 2 pixel cells: cell 0 covers source [0, 0.75) and would
        // slice to nothing; every cell must show one pixel.
        var image = new TerminalImage(new byte[3 * 4], 3, 1, cellWidth: 2, cellHeight: 3);
        var placement = new ImagePlacement(image, 0, 0, 0, 3, 1, 4, 1, ImageScaling.Stretched);

        var tiles = Enumerable.Range(0, 4)
            .Select(col => placement.TryGetTileSource(col, 0, out var x, out _, out var width, out _)
                ? (X: x, Width: width)
                : (X: -1, Width: 0))
            .ToArray();

        Assert.Equal(new[] { (0, 1), (0, 1), (1, 1), (2, 1) }, tiles.Select(t => (t.X, t.Width)));
    }

    [Fact]
    public void Slicing_a_run_keeps_the_scaling_context()
    {
        // Sixel runs are split when text prints into them; the surviving parts must keep drawing
        // at the same scale as the whole did.
        var placement = new LinePlacement(
            imageId: 7, column: 0, cols: 10,
            srcX: 0, srcY: 0, srcWidth: 40, srcHeight: 3,
            pxPerCellX: 4f, pxPerCellY: 3f);

        var right = placement.TruncatedAfter(3);

        Assert.Equal(4f, right.PxPerCellX);
        Assert.Equal(3f, right.PxPerCellY);
    }
}
