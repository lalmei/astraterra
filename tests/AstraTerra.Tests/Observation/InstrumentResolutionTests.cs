using AstraTerra.Observation;
using Xunit;

namespace AstraTerra.Tests.Observation;

public sealed class InstrumentResolutionTests
{
    [Fact]
    public void Truncate_Cuts_A_Reading_Down_To_The_Scale()
    {
        Assert.Equal(34.0, InstrumentResolution.Truncate(34.87, InstrumentResolution.CrossStaffDeg), 9);
        Assert.Equal(34.75, InstrumentResolution.Truncate(34.87, InstrumentResolution.QuadrantDeg), 9);
        Assert.Equal(34 + (52.0 / 60.0), InstrumentResolution.Truncate(34.87, InstrumentResolution.BrassSextantDeg), 9);
    }

    [Fact]
    public void Truncate_Never_Reads_Further_From_The_Horizon_Than_The_Body_Stood()
    {
        Assert.Equal(-34.0, InstrumentResolution.Truncate(-34.87, InstrumentResolution.CrossStaffDeg), 9);
    }

    [Fact]
    public void Truncate_Keeps_An_Angle_Sitting_Exactly_On_A_Graduation()
    {
        Assert.Equal(34.25, InstrumentResolution.Truncate(34.25, InstrumentResolution.QuadrantDeg), 9);
        Assert.Equal(21.0 + (32.0 / 60.0), InstrumentResolution.Truncate(21.0 + (32.0 / 60.0), InstrumentResolution.BrassSextantDeg), 9);
    }

    [Fact]
    public void Truncate_Leaves_A_Reading_Alone_When_The_Instrument_Has_No_Scale()
    {
        Assert.Equal(34.87, InstrumentResolution.Truncate(34.87, 0.0), 9);
    }

    [Fact]
    public void Format_Writes_Only_The_Digits_The_Instrument_Earned()
    {
        Assert.Equal("34°", InstrumentResolution.Format(34.87, InstrumentResolution.CrossStaffDeg));
        Assert.Equal("34° 45′", InstrumentResolution.Format(34.87, InstrumentResolution.QuadrantDeg));
        Assert.Equal("34° 52′", InstrumentResolution.Format(34.87, InstrumentResolution.BrassSextantDeg));
        Assert.Equal("34° 52.0′", InstrumentResolution.Format(34.87, InstrumentResolution.MuralQuadrantDeg));
    }

    [Fact]
    public void Format_Signs_An_Altitude_And_Leaves_A_Bearing_Unsigned()
    {
        Assert.Equal("+34°", InstrumentResolution.Format(34.87, InstrumentResolution.CrossStaffDeg, signed: true));
        Assert.Equal("-3°", InstrumentResolution.Format(-3.4, InstrumentResolution.CrossStaffDeg, signed: true));
        Assert.Equal("118°", InstrumentResolution.Format(118.6, InstrumentResolution.CrossStaffDeg));
    }
}
