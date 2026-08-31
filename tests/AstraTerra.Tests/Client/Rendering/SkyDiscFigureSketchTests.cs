using AstraTerra.Astronomy;
using AstraTerra.Client.Rendering;
using AstraTerra.Constellations;
using Xunit;

namespace AstraTerra.Tests.Client.Rendering;

public sealed class SkyDiscFigureSketchTests
{
    [Fact]
    public void The_Stored_Figure_Becomes_Lines_And_Star_Punches()
    {
        var figure = Figure((10, 20), (20, 30));
        var catalog = Catalog(
            Star(10, 350, 5),
            Star(20, 0, 10),
            Star(30, 10, 2));

        var sketch = SkyDiscFigureSketch.Project(figure, catalog);

        Assert.Equal(2, sketch.Lines.Count);
        Assert.Equal([10, 20, 30], sketch.Stars.Select(star => star.Hip));
        Assert.All(
            sketch.Stars,
            star =>
            {
                Assert.InRange(star.X, -SkyDiscFigureSketch.Radius, SkyDiscFigureSketch.Radius);
                Assert.InRange(star.Y, -SkyDiscFigureSketch.Radius, SkyDiscFigureSketch.Radius);
            });
    }

    [Fact]
    public void A_Figure_Across_Zero_Right_Ascension_Stays_Joined()
    {
        var figure = Figure((1, 2), (2, 3));
        var sketch = SkyDiscFigureSketch.Project(
            figure,
            Catalog(Star(1, 359, 0), Star(2, 0, 2), Star(3, 1, 0)));

        var byHip = sketch.Stars.ToDictionary(star => star.Hip);

        var outerSpan = Math.Abs(byHip[1].X - byHip[3].X);
        Assert.InRange(byHip[2].X, Math.Min(byHip[1].X, byHip[3].X), Math.Max(byHip[1].X, byHip[3].X));
        Assert.True(Math.Abs(byHip[1].X - byHip[2].X) < outerSpan * 0.75);
        Assert.True(Math.Abs(byHip[2].X - byHip[3].X) < outerSpan * 0.75);
    }

    [Fact]
    public void North_Remains_Above_South_On_The_Disc()
    {
        var sketch = SkyDiscFigureSketch.Project(
            Figure((1, 2)),
            Catalog(Star(1, 45, -10), Star(2, 45, 20)));
        var byHip = sketch.Stars.ToDictionary(star => star.Hip);

        Assert.True(byHip[2].Y > byHip[1].Y);
    }

    [Fact]
    public void Edges_Whose_Catalog_Stars_Are_Gone_Are_Not_Invented()
    {
        var sketch = SkyDiscFigureSketch.Project(
            Figure((1, 2), (2, 3)),
            Catalog(Star(1, 0, 0), Star(2, 10, 0)));

        Assert.Single(sketch.Lines);
        Assert.Equal([1, 2], sketch.Stars.Select(star => star.Hip));
    }

    [Fact]
    public void A_Blank_Figure_Or_Missing_Catalog_Draws_Nothing()
    {
        Assert.Empty(SkyDiscFigureSketch.Project(new SkyDiscFigure(), Catalog()).Lines);
        Assert.Empty(SkyDiscFigureSketch.Project(Figure((1, 2)), (StarCatalog?)null).Lines);
    }

    private static SkyDiscFigure Figure(params (int A, int B)[] edges)
    {
        var figure = new SkyDiscFigure();
        foreach (var (a, b) in edges)
        {
            figure.Engrave(a, b, 1);
        }

        return figure;
    }

    private static StarCatalog Catalog(params StarCatalogEntry[] stars) => new(stars, []);

    private static StarCatalogEntry Star(int hip, double rightAscensionDeg, double declinationDeg)
        => new(hip, rightAscensionDeg, declinationDeg, 1.0, null, false);
}
