using AstraTerra.Astronomy;
using AstraTerra.Commands;
using Xunit;

namespace AstraTerra.Tests.Commands;

public sealed class CometReportTests
{
    private const int DaysPerYear = 100;

    private static CometCatalog Catalog()
        => new(1, [
            new CometEntry(
                "test",
                "Test",
                PeriodYears: 10.0,
                FirstPerihelionYear: 2.0,
                WindowHalfWidthYears: 0.1,
                PeakMagnitude: -1.0,
                EdgeMagnitude: 8.0,
                BrighteningExponent: 0.5,
                PeakTailLengthDeg: 20.0,
                Path: [new CometPathKeyframe(0.0, 123.4, -12.3)],
                TintR: 1f,
                TintG: 1f,
                TintB: 1f)
        ]);

    [Fact]
    public void A_Comet_That_Is_Up_Reports_What_It_Is_Doing()
    {
        var report = CometReport.Describe(Catalog(), 2.0 * DaysPerYear, DaysPerYear);

        Assert.Contains("test (Test)", report);
        Assert.Contains("up", report);
        Assert.Contains("magnitude -1.0", report);
        Assert.Contains("tail 20.0 deg", report);
        Assert.Contains("ra 123.4 deg", report);
    }

    [Fact]
    public void A_Comet_That_Is_Away_Reports_When_It_Returns()
    {
        var report = CometReport.Describe(Catalog(), 5.0 * DaysPerYear, DaysPerYear);

        Assert.Contains("away", report);
        Assert.Contains("returns in 690 days", report);
        Assert.Contains("world year 11.90", report);
        Assert.Contains("period 10.0 y", report);
    }

    [Fact]
    public void No_Catalog_Says_So_Rather_Than_Reporting_An_Empty_Sky()
    {
        Assert.Contains("No comet catalog", CometReport.Describe(null, 100.0, DaysPerYear));
        Assert.Contains("No comet catalog", CometReport.Describe(new CometCatalog(1, []), 100.0, DaysPerYear));
    }
}
