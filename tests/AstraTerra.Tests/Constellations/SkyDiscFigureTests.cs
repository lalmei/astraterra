using AstraTerra.Constellations;
using Vintagestory.API.Datastructures;
using Xunit;

namespace AstraTerra.Tests.Constellations;

public sealed class SkyDiscFigureTests
{
    private const int Metal = 1;
    private const int NoRoomForAny = 0;

    [Fact]
    public void The_First_Line_Goes_Anywhere()
    {
        var figure = new SkyDiscFigure();

        var result = figure.Engrave(100, 200, Metal);

        Assert.Equal(SkyDiscEngraveOutcome.Engraved, result.Outcome);
        Assert.Single(figure.Edges);
        Assert.True(figure.IsOnePiece);
    }

    [Fact]
    public void A_Line_That_Touches_The_Figure_Joins_It()
    {
        var figure = new SkyDiscFigure();
        figure.Engrave(100, 200, Metal);

        // From either end, and in either direction: what matters is that it meets what is there.
        Assert.True(figure.Engrave(200, 300, Metal).Changed);
        Assert.True(figure.Engrave(400, 100, Metal).Changed);

        Assert.Equal(3, figure.Edges.Count);
        Assert.True(figure.IsOnePiece);
    }

    [Fact]
    public void A_Line_Struck_Somewhere_Else_Is_A_Second_Constellation()
    {
        var figure = new SkyDiscFigure();
        figure.Engrave(100, 200, Metal);

        var result = figure.Engrave(500, 600, Metal);

        Assert.Equal(SkyDiscEngraveOutcome.NoRoom, result.Outcome);
        Assert.Equal(SkyDiscFigure.NoRoomMessage, result.Message);

        // Refused means untouched: the disc is exactly as it was, not half-engraved.
        Assert.Single(figure.Edges);
        Assert.True(figure.IsOnePiece);
    }

    [Fact]
    public void The_Same_Line_Does_Not_Go_On_Twice()
    {
        var figure = new SkyDiscFigure();
        figure.Engrave(100, 200, Metal);

        // Including drawn back the other way, which is the same line.
        Assert.Equal(SkyDiscEngraveOutcome.AlreadyThere, figure.Engrave(200, 100, Metal).Outcome);
        Assert.Single(figure.Edges);
    }

    [Fact]
    public void A_Line_Runs_Between_Two_Stars()
    {
        var figure = new SkyDiscFigure();

        Assert.Equal(SkyDiscEngraveOutcome.SameStar, figure.Engrave(100, 100, Metal).Outcome);
        Assert.Empty(figure.Edges);
    }

    [Fact]
    public void A_Disc_With_No_Room_Takes_Nothing()
    {
        var figure = new SkyDiscFigure();

        Assert.Equal(SkyDiscEngraveOutcome.NotEngravable, figure.Engrave(100, 200, NoRoomForAny).Outcome);
        Assert.Empty(figure.Edges);
    }

    [Fact]
    public void Fired_Clay_Holds_Its_Figure_But_Will_Not_Take_Another()
    {
        // It has room for one — it is carrying one — and is simply too hard to work now. Which is a
        // different answer from "this disc will not hold a figure", and has to read as one.
        var figure = new SkyDiscFigure();
        figure.Engrave(100, 200, Metal);

        var result = figure.Engrave(200, 300, Metal, workable: false);

        Assert.Equal(SkyDiscEngraveOutcome.TooHard, result.Outcome);
        Assert.Contains("before it is baked", result.Message, StringComparison.Ordinal);
        Assert.Single(figure.Edges);
    }

    [Fact]
    public void Soft_Clay_Takes_A_Figure_The_Way_Metal_Does()
    {
        var figure = new SkyDiscFigure();

        Assert.True(figure.Engrave(100, 200, Metal, workable: true).Changed);
        Assert.True(figure.Engrave(200, 300, Metal, workable: true).Changed);
        Assert.True(figure.IsOnePiece);
    }

    [Fact]
    public void A_Line_Can_Be_Taken_Off_Again_Even_If_It_Leaves_Two_Pieces()
    {
        var figure = new SkyDiscFigure();
        figure.Engrave(100, 200, Metal);
        figure.Engrave(200, 300, Metal);
        figure.Engrave(300, 400, Metal);

        // Erasing the middle of a chain splits it. That is allowed on the way out even though it is
        // refused on the way in, so a figure cannot be drawn into a corner it cannot leave.
        Assert.True(figure.Erase(200, 300));
        Assert.False(figure.IsOnePiece);

        // And it can be joined back up.
        Assert.True(figure.Engrave(200, 300, Metal).Changed);
        Assert.True(figure.IsOnePiece);
    }

    [Fact]
    public void Erasing_A_Line_That_Is_Not_There_Changes_Nothing()
    {
        var figure = new SkyDiscFigure();
        figure.Engrave(100, 200, Metal);

        Assert.False(figure.Erase(500, 600));
        Assert.Single(figure.Edges);
    }

    [Fact]
    public void A_Figure_Survives_Being_Put_Down_And_Picked_Up()
    {
        var figure = new SkyDiscFigure();
        figure.Engrave(100, 200, Metal);
        figure.Engrave(200, 300, Metal);

        var attributes = new TreeAttribute();
        SkyDiscFigureStore.Write(attributes, figure);
        var reread = SkyDiscFigureStore.Read(attributes);

        Assert.NotNull(reread);
        Assert.Equal(
            figure.Edges.Select(edge => (edge.A, edge.B)),
            reread!.Edges.Select(edge => (edge.A, edge.B)));

        // And it keeps its rule: a line that joined nothing is still refused after a round trip.
        Assert.Equal(SkyDiscEngraveOutcome.NoRoom, reread.Engrave(500, 600, Metal).Outcome);
        Assert.True(reread.Engrave(300, 400, Metal).Changed);
    }

    [Fact]
    public void A_Blank_Disc_Reads_As_A_Blank_Figure_Rather_Than_Nothing()
    {
        Assert.Null(SkyDiscFigureStore.Read(new TreeAttribute()));
        Assert.True(SkyDiscFigureStore.ReadOrEmpty(null).IsBlank);
    }

    [Fact]
    public void An_Unreadable_Figure_Is_A_Blank_Disc_Rather_Than_A_Broken_One()
    {
        // The solar band on the same disc is still worth reading, and a disc that throws while being
        // drawn cannot be looked at at all.
        var attributes = new TreeAttribute();
        attributes.SetString(SkyDiscFigureStore.FigureJsonAttribute, "{ not json");

        Assert.Null(SkyDiscFigureStore.Read(attributes));
    }
}
