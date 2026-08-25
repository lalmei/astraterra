using AstraTerra.Observation;
using Xunit;

namespace AstraTerra.Tests.Observation;

public sealed class ObservationProvenanceTests
{
    [Fact]
    public void An_Answer_With_Nothing_Behind_It_Says_So()
    {
        Assert.Equal(ObservationConfidence.None, ObservationProvenance.Grade([]));
        Assert.Contains("without sightings", ObservationProvenance.Describe([]));
    }

    [Fact]
    public void One_Night_Is_Enough_To_Point_At_And_Not_To_Predict_From()
    {
        var records = Sightings(412);

        Assert.Equal(ObservationConfidence.Low, ObservationProvenance.Grade(records));
        Assert.Equal("from 1 sighting, to 0° 01′", ObservationProvenance.Describe(records));
    }

    [Fact]
    public void Two_Nights_Are_Fair_And_Say_How_Far_Apart_They_Were()
    {
        var records = Sightings(412, 415);

        Assert.Equal(ObservationConfidence.Fair, ObservationProvenance.Grade(records));
        Assert.Equal("from 2 sightings over 3 days, to 0° 01′", ObservationProvenance.Describe(records));
    }

    [Fact]
    public void Several_Nights_Across_A_Real_Span_Are_Worth_Extrapolating()
    {
        Assert.Equal(ObservationConfidence.Good, ObservationProvenance.Grade(Sightings(412, 415, 423)));
    }

    [Fact]
    public void Several_Nights_Crammed_Into_One_Are_Not()
    {
        Assert.Equal(ObservationConfidence.Fair, ObservationProvenance.Grade(Sightings(412, 412, 412)));
    }

    [Fact]
    public void The_Coarsest_Instrument_In_The_Set_Is_The_One_Reported()
    {
        var log = new ObservationLog();
        log.Record(34.2, 118.0, 1.4, 412, 21.5, 51.0, InstrumentResolution.BrassSextantDeg, siderealAngleDeg: 90.0);
        log.Record(34.2, 118.0, 1.4, 415, 21.5, 51.0, InstrumentResolution.CrossStaffDeg, siderealAngleDeg: 90.0);

        Assert.Contains("to 1°", ObservationProvenance.Describe(log.Observations));
    }

    private static IReadOnlyList<ObservationRecord> Sightings(params int[] days)
    {
        var log = new ObservationLog();
        foreach (var day in days)
        {
            log.Record(34.2, 118.0, 1.4, day, 21.5, 51.0, InstrumentResolution.BrassSextantDeg, siderealAngleDeg: 90.0);
        }

        return log.Observations;
    }
}
