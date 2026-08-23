using AstraTerra.Observation;
using Xunit;

namespace AstraTerra.Tests.Observation;

public sealed class ObservationLogTests
{
    [Fact]
    public void Record_Numbers_Entries_In_The_Order_They_Were_Written()
    {
        var log = new ObservationLog();

        var first = log.Record(34.2, 118.0, 1.4, 412, 21.5, 51.0, InstrumentResolution.BrassSextantDeg);
        var second = log.Record(35.0, 119.0, 1.4, 413, 21.5, 51.0, InstrumentResolution.BrassSextantDeg);

        Assert.Equal(1, first.Id);
        Assert.Equal(2, second.Id);
        Assert.Equal(2, log.Observations.Count);
    }

    [Fact]
    public void Record_Truncates_Angles_To_What_The_Instrument_Reads()
    {
        var log = new ObservationLog();

        var record = log.Record(34.87, 118.99, null, 412, 21.5, 51.0, InstrumentResolution.CrossStaffDeg);

        Assert.Equal(34.0, record.AltitudeDeg, 9);
        Assert.Equal(118.0, record.AzimuthDeg, 9);
    }

    [Fact]
    public void Record_Keeps_Two_Sightings_Of_The_Same_Position_Apart()
    {
        var log = new ObservationLog();

        log.Record(34.2, 118.0, 1.4, 412, 21.5, 51.0, InstrumentResolution.BrassSextantDeg);
        log.Record(34.2, 118.0, 1.4, 480, 21.5, 51.0, InstrumentResolution.BrassSextantDeg);

        Assert.Equal([412, 480], log.Observations.Select(record => record.Day));
    }

    [Fact]
    public void Describe_Reads_As_A_Dated_Entry_And_Claims_No_Identity()
    {
        var record = new ObservationLog()
            .Record(34.2, 118.0, 1.4, 412, 21.5, 51.0, InstrumentResolution.BrassSextantDeg);

        Assert.Equal("day 412, 21:30 — alt +34° 12′, az 118° 00′, mag 1.4", record.Describe());
    }

    [Fact]
    public void Describe_Says_Nothing_About_Brightness_When_There_Was_No_Scale_To_Judge_It()
    {
        var record = new ObservationLog()
            .Record(34.2, 118.0, null, 412, 21.5, 51.0, InstrumentResolution.BrassSextantDeg);

        Assert.DoesNotContain("mag", record.Describe());
    }

    [Fact]
    public void Round_Trips_Through_Json()
    {
        var log = new ObservationLog();
        log.Record(34.2, 118.0, 1.4, 412, 21.5, 51.0, InstrumentResolution.BrassSextantDeg);
        log.Record(-2.0, 4.0, null, 480, 5.25, 51.0, InstrumentResolution.CrossStaffDeg);

        var loaded = ObservationLogPersistence.Deserialize(ObservationLogPersistence.Serialize(log));

        Assert.Equal(log.Observations, loaded.Observations);
        Assert.Equal(3, loaded.Record(1.0, 1.0, null, 481, 5.0, 51.0, InstrumentResolution.CrossStaffDeg).Id);
    }

    [Fact]
    public void Remove_Strikes_One_Entry_Out()
    {
        var log = new ObservationLog();
        var first = log.Record(34.2, 118.0, 1.4, 412, 21.5, 51.0, InstrumentResolution.BrassSextantDeg);
        log.Record(35.0, 119.0, 1.4, 413, 21.5, 51.0, InstrumentResolution.BrassSextantDeg);

        Assert.True(log.Remove(first.Id));
        Assert.False(log.Remove(first.Id));
        Assert.Equal(2, log.Observations.Single().Id);
    }
}
