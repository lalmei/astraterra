using AstraTerra.Astronomy;
using Xunit;

namespace AstraTerra.Tests.Astronomy;

public sealed class MeteorShowerActivityTests
{
    private const double NightDarkness = 1.0;
    private const double NewMoon = 0.0;
    private const double FullMoon = 1.0;

    private static readonly IReadOnlyList<MeteorShowerEntry> Catalog =
        MeteorShowerCatalogLoader.Parse(File.ReadAllText("assets/astraterra/data/meteor-showers.v1.json"));

    private static MeteorShowerEntry Perseids => Catalog.Single(shower => shower.Id == "PER");

    // ---------------------------------------------------------------------------------------
    // The headline property: a shower is anchored to a point in the world's orbit, not to a day
    // number, so it keeps its season however long the world's year is.
    // ---------------------------------------------------------------------------------------

    [Theory]
    [InlineData(12)]
    [InlineData(108)]
    [InlineData(360)]
    public void Every_Shower_Peaks_At_The_Same_Point_In_The_Year_On_Any_World(int daysPerYear)
    {
        foreach (var shower in Catalog)
        {
            var peakYearFraction = FindPeakYearFraction(shower, daysPerYear);

            Assert.Equal(shower.PeakSolarLongitudeDeg / 360.0, peakYearFraction, 3);
        }
    }

    [Fact]
    public void A_Shower_Keeps_Its_Season_On_A_Twelve_Day_Year_And_A_Three_Hundred_Sixty_Day_One()
    {
        var shower = Perseids;

        var shortYearFraction = FindPeakYearFraction(shower, 12);
        var longYearFraction = FindPeakYearFraction(shower, 360);

        Assert.Equal(longYearFraction, shortYearFraction, 3);
        Assert.Equal(SeasonIndex(longYearFraction), SeasonIndex(shortYearFraction));

        // Which is the whole point: the same season is a wildly different day number on each world,
        // so anchoring the peak to a day of the year cannot survive both.
        var shortYearPeakDay = shortYearFraction * 12;
        var longYearPeakDay = longYearFraction * 360;
        Assert.InRange(shortYearPeakDay, 4.0, 5.0);
        Assert.InRange(longYearPeakDay, 139.0, 141.0);
    }

    [Fact]
    public void Showers_Stay_Spread_Across_The_Year_Rather_Than_Clumping_On_A_Short_Year()
    {
        var shortYear = Catalog.Select(shower => SeasonIndex(FindPeakYearFraction(shower, 12))).ToList();
        var longYear = Catalog.Select(shower => SeasonIndex(FindPeakYearFraction(shower, 360))).ToList();

        Assert.Equal(longYear, shortYear);
        Assert.Equal(4, longYear.Distinct().Count());
    }

    [Fact]
    public void A_Shower_Is_Active_For_The_Same_Share_Of_The_Year_On_Any_World()
    {
        var shower = Perseids;
        var expectedShare = 2.0 * shower.WindowHalfWidthDeg / 360.0;

        foreach (var daysPerYear in new[] { 12, 108, 360 })
        {
            Assert.Equal(expectedShare, MeasureActiveYearShare(shower, daysPerYear), 3);
        }
    }

    // ---------------------------------------------------------------------------------------
    // Window shape
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void Seasonal_Factor_Peaks_At_Maximum_And_Falls_To_Zero_At_The_Window_Edges()
    {
        var shower = TestShower(peakSolarLongitudeDeg: 140.0, windowHalfWidthDeg: 18.0);

        Assert.Equal(1.0, MeteorShowerActivity.GetSeasonalFactor(shower, 140.0), 9);

        var previous = 1.0;
        for (var separation = 0.25; separation <= 18.0; separation += 0.25)
        {
            var after = MeteorShowerActivity.GetSeasonalFactor(shower, 140.0 + separation);
            var before = MeteorShowerActivity.GetSeasonalFactor(shower, 140.0 - separation);

            Assert.Equal(after, before, 9);
            Assert.True(
                after < previous,
                $"Rate must keep falling away from the peak, but {separation:0.##} deg out gave {after:0.#####} after {previous:0.#####}.");
            previous = after;
        }

        Assert.Equal(0.0, MeteorShowerActivity.GetSeasonalFactor(shower, 140.0 + 18.0));
        Assert.Equal(0.0, MeteorShowerActivity.GetSeasonalFactor(shower, 140.0 - 18.0));
    }

    [Theory]
    [InlineData(160.0)]
    [InlineData(200.0)]
    [InlineData(320.0)]
    [InlineData(0.0)]
    [InlineData(100.0)]
    public void Seasonal_Factor_Is_Zero_Outside_The_Window(double solarLongitudeDeg)
    {
        var shower = TestShower(peakSolarLongitudeDeg: 140.0, windowHalfWidthDeg: 18.0);

        Assert.Equal(0.0, MeteorShowerActivity.GetSeasonalFactor(shower, solarLongitudeDeg));
    }

    [Fact]
    public void Seasonal_Factor_Is_Sharply_Peaked_Rather_Than_Flat_Across_Its_Window()
    {
        var shower = TestShower(peakSolarLongitudeDeg: 140.0, windowHalfWidthDeg: 18.0);

        // Halfway to the edge the shower should already be a shadow of its peak, so the good night
        // is a specific night rather than a fortnight of the same thing.
        Assert.InRange(MeteorShowerActivity.GetSeasonalFactor(shower, 149.0), 0.05, 0.15);
    }

    // ---------------------------------------------------------------------------------------
    // Wrap-around: a window straddling 0/360 deg
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void A_Window_Straddling_The_Wrap_Behaves_Exactly_Like_One_That_Does_Not()
    {
        var wrapped = TestShower(peakSolarLongitudeDeg: 2.0, windowHalfWidthDeg: 8.0);
        var plain = TestShower(peakSolarLongitudeDeg: 182.0, windowHalfWidthDeg: 8.0);

        for (var offset = -20.0; offset <= 20.0; offset += 0.25)
        {
            var wrappedFactor = MeteorShowerActivity.GetSeasonalFactor(
                wrapped,
                CelestialMath.NormalizeDegrees(2.0 + offset));
            var plainFactor = MeteorShowerActivity.GetSeasonalFactor(
                plain,
                CelestialMath.NormalizeDegrees(182.0 + offset));

            Assert.Equal(plainFactor, wrappedFactor, 12);
        }
    }

    [Fact]
    public void A_Shower_Peaking_Just_After_The_Wrap_Is_Active_Just_Before_It()
    {
        var shower = TestShower(peakSolarLongitudeDeg: 2.0, windowHalfWidthDeg: 8.0);

        Assert.True(MeteorShowerActivity.GetSeasonalFactor(shower, 358.0) > 0.0);
        Assert.True(MeteorShowerActivity.GetSeasonalFactor(shower, 359.9) > MeteorShowerActivity.GetSeasonalFactor(shower, 356.0));
        Assert.Equal(0.0, MeteorShowerActivity.GetSeasonalFactor(shower, 353.0));
        Assert.Equal(0.0, MeteorShowerActivity.GetSeasonalFactor(shower, 180.0));
    }

    [Fact]
    public void A_Wrapping_Window_Is_Active_For_The_Same_Span_As_A_Plain_One()
    {
        var wrapped = TestShower(peakSolarLongitudeDeg: 359.0, windowHalfWidthDeg: 8.0);
        var plain = TestShower(peakSolarLongitudeDeg: 179.0, windowHalfWidthDeg: 8.0);

        Assert.Equal(MeasureActiveSpanDegrees(plain), MeasureActiveSpanDegrees(wrapped), 2);
        Assert.Equal(16.0, MeasureActiveSpanDegrees(wrapped), 2);
    }

    // ---------------------------------------------------------------------------------------
    // Radiant altitude
    // ---------------------------------------------------------------------------------------

    [Theory]
    [InlineData(-90.0)]
    [InlineData(-40.0)]
    [InlineData(-10.0)]
    [InlineData(-0.5)]
    [InlineData(0.0)]
    public void Nothing_Is_Seen_While_The_Radiant_Is_At_Or_Below_The_Horizon(double altitudeDeg)
    {
        var reading = ReadAtPeak(altitudeDeg);

        Assert.Equal(0.0, reading.RadiantFactor);
        Assert.Equal(0.0, reading.ObservedHourlyRate);
    }

    [Fact]
    public void Rate_Climbs_With_The_Radiant_All_The_Way_To_The_Zenith()
    {
        var previous = 0.0;
        for (var altitudeDeg = 1.0; altitudeDeg <= 90.0; altitudeDeg += 1.0)
        {
            var rate = ReadAtPeak(altitudeDeg).ObservedHourlyRate;

            Assert.True(
                rate > previous,
                $"Rate must climb with the radiant, but {altitudeDeg:0.#} deg gave {rate:0.###} after {previous:0.###}.");
            previous = rate;
        }

        Assert.Equal(Math.Sin(30.0 * Math.PI / 180.0), ReadAtPeak(30.0).RadiantFactor, 9);
        Assert.Equal(1.0, ReadAtPeak(90.0).RadiantFactor, 9);
    }

    [Fact]
    public void A_Radiant_Overhead_At_The_Peak_Under_A_Dark_New_Moon_Sky_Delivers_The_Published_Zhr()
    {
        var reading = ReadAtPeak(90.0);

        Assert.Equal(TestShower().PeakZenithHourlyRate, reading.ObservedHourlyRate, 6);
    }

    // ---------------------------------------------------------------------------------------
    // Moonlight and daylight
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void A_Full_Moon_Suppresses_The_Rate_Well_Below_A_New_Moon()
    {
        var newMoonRate = ReadAtPeak(60.0, moonBrightness: NewMoon).ObservedHourlyRate;
        var fullMoonRate = ReadAtPeak(60.0, moonBrightness: FullMoon).ObservedHourlyRate;

        Assert.True(newMoonRate > 0.0);
        Assert.True(
            fullMoonRate < newMoonRate * 0.25,
            $"A full moon must ruin the shower, but it only fell from {newMoonRate:0.##} to {fullMoonRate:0.##}.");
        Assert.True(fullMoonRate > 0.0, "The brightest meteors still get through a full moon.");
    }

    [Fact]
    public void Rate_Falls_Off_Steadily_As_The_Moon_Fills()
    {
        var previous = double.MaxValue;
        for (var brightness = 0.0; brightness <= 1.0; brightness += 0.05)
        {
            var rate = ReadAtPeak(60.0, moonBrightness: brightness).ObservedHourlyRate;

            Assert.True(
                rate < previous,
                $"Rate must fall as the moon fills, but brightness {brightness:0.##} gave {rate:0.###} after {previous:0.###}.");
            previous = rate;
        }
    }

    [Fact]
    public void Daylight_Puts_A_Shower_Out_And_Dusk_Brings_It_Back()
    {
        Assert.Equal(0.0, ReadAtPeak(60.0, skyDarkness: 0.0).ObservedHourlyRate);
        Assert.Equal(0.0, ReadAtPeak(60.0, skyDarkness: MeteorShowerActivity.MinimumDarkness).ObservedHourlyRate);

        var previous = 0.0;
        for (var darkness = MeteorShowerActivity.MinimumDarkness + 0.05; darkness <= 1.0; darkness += 0.05)
        {
            var rate = ReadAtPeak(60.0, skyDarkness: darkness).ObservedHourlyRate;

            Assert.True(
                rate > previous,
                $"Rate must rise as the sky darkens, but darkness {darkness:0.##} gave {rate:0.###} after {previous:0.###}.");
            previous = rate;
        }

        Assert.Equal(1.0, ReadAtPeak(60.0, skyDarkness: 1.0).DarknessFactor, 9);
    }

    // ---------------------------------------------------------------------------------------
    // Whole-sky reading
    // ---------------------------------------------------------------------------------------

    [Theory]
    [InlineData(12)]
    [InlineData(108)]
    [InlineData(360)]
    public void The_Perseids_Are_Best_At_Their_Own_Solar_Longitude_From_A_Northern_Latitude(int daysPerYear)
    {
        const double latitudeDeg = 45.0;
        const double hoursPerDay = 24.0;
        var shower = Perseids;

        var bestRate = 0.0;
        var bestSolarLongitudeDeg = double.NaN;
        var samples = daysPerYear * 24;
        for (var sample = 0; sample < samples; sample++)
        {
            var totalDays = sample * (double)daysPerYear / samples;
            var rate = ReadFromWorldClock(shower, totalDays, daysPerYear, hoursPerDay, latitudeDeg).ObservedHourlyRate;
            if (rate > bestRate)
            {
                bestRate = rate;
                bestSolarLongitudeDeg = CelestialMath.GetSolarLongitudeDegrees(totalDays, daysPerYear);
            }
        }

        Assert.True(bestRate > 0.0, "The Perseids must be visible at some point in the year.");
        Assert.InRange(
            Math.Abs(CelestialMath.ShortestAngularDistanceDegrees(shower.PeakSolarLongitudeDeg, bestSolarLongitudeDeg)),
            0.0,
            shower.WindowHalfWidthDeg);

        // And nothing at all on the far side of the year, whatever the sky is doing.
        var midwinter = MeteorShowerActivity.Read(
            shower,
            CelestialMath.NormalizeDegrees(shower.PeakSolarLongitudeDeg + 180.0),
            new HorizontalCoordinates(AzimuthDeg: 0.0, AltitudeDeg: 80.0),
            NightDarkness,
            NewMoon);
        Assert.Equal(0.0, midwinter.ObservedHourlyRate);
    }

    [Fact]
    public void Reading_A_Shower_From_The_World_Clock_Places_Its_Radiant_Like_Any_Fixed_Star()
    {
        var shower = Perseids;
        const double latitudeDeg = 45.0;
        const double localSiderealDeg = 90.0;

        var expected = CelestialMath.GetHorizontalCoordinates(
            shower.RightAscensionDeg,
            shower.DeclinationDeg,
            latitudeDeg,
            localSiderealDeg);
        var reading = MeteorShowerActivity.Read(
            shower,
            shower.PeakSolarLongitudeDeg,
            latitudeDeg,
            localSiderealDeg,
            NightDarkness,
            NewMoon);

        Assert.Equal(expected.AzimuthDeg, reading.AzimuthDeg, 9);
        Assert.Equal(expected.AltitudeDeg, reading.AltitudeDeg, 9);
    }

    [Fact]
    public void ReadAll_Returns_Every_Shower_With_The_Strongest_First()
    {
        var readings = MeteorShowerActivity.ReadAll(
            Catalog,
            Perseids.PeakSolarLongitudeDeg,
            latitudeDeg: 45.0,
            localSiderealDeg: 60.0,
            NightDarkness,
            NewMoon);

        Assert.Equal(Catalog.Count, readings.Count);
        Assert.Equal("PER", readings[0].Shower.Id);
        Assert.True(readings[0].ObservedHourlyRate > 0.0);
        Assert.Equal(
            readings.Select(reading => reading.ObservedHourlyRate).OrderByDescending(rate => rate),
            readings.Select(reading => reading.ObservedHourlyRate));
    }

    // ---------------------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------------------

    private static MeteorShowerEntry TestShower(
        double peakSolarLongitudeDeg = 140.0,
        double windowHalfWidthDeg = 18.0,
        double peakZenithHourlyRate = 100.0)
        => new("TST", "Test shower", 0.0, 0.0, peakSolarLongitudeDeg, windowHalfWidthDeg, peakZenithHourlyRate);

    private static MeteorShowerReading ReadAtPeak(
        double radiantAltitudeDeg,
        double skyDarkness = NightDarkness,
        double moonBrightness = NewMoon)
    {
        var shower = TestShower();
        return MeteorShowerActivity.Read(
            shower,
            shower.PeakSolarLongitudeDeg,
            new HorizontalCoordinates(AzimuthDeg: 0.0, radiantAltitudeDeg),
            skyDarkness,
            moonBrightness);
    }

    private static MeteorShowerReading ReadFromWorldClock(
        MeteorShowerEntry shower,
        double totalDays,
        int daysPerYear,
        double hoursPerDay,
        double latitudeDeg)
    {
        var localSiderealDeg = CelestialMath.GetVanillaAlignedLocalSiderealAngle(totalDays, daysPerYear, hoursPerDay);
        var solarLongitudeDeg = CelestialMath.GetSolarLongitudeDegrees(totalDays, daysPerYear);

        // Stand in for the game's daylight: fully dark at midnight, full daylight at noon.
        var localHours = CelestialMath.GetLocalSolarTimeHours(totalDays, hoursPerDay);
        var skyDarkness = (1.0 + Math.Cos((localHours / hoursPerDay) * 2.0 * Math.PI)) / 2.0;

        return MeteorShowerActivity.Read(shower, solarLongitudeDeg, latitudeDeg, localSiderealDeg, skyDarkness, NewMoon);
    }

    /// <summary>Where in the world year the shower is strongest, as a fraction of the year.</summary>
    private static double FindPeakYearFraction(MeteorShowerEntry shower, int daysPerYear)
    {
        const int samples = 20000;
        var bestFactor = -1.0;
        var bestFraction = double.NaN;

        for (var sample = 0; sample < samples; sample++)
        {
            var fraction = sample / (double)samples;
            var factor = MeteorShowerActivity.GetSeasonalFactor(
                shower,
                CelestialMath.GetSolarLongitudeDegrees(fraction * daysPerYear, daysPerYear));
            if (factor > bestFactor)
            {
                bestFactor = factor;
                bestFraction = fraction;
            }
        }

        return bestFraction;
    }

    /// <summary>What share of the world year the shower produces anything at all.</summary>
    private static double MeasureActiveYearShare(MeteorShowerEntry shower, int daysPerYear)
    {
        const int samples = 20000;
        var active = 0;

        for (var sample = 0; sample < samples; sample++)
        {
            var solarLongitudeDeg = CelestialMath.GetSolarLongitudeDegrees(
                sample / (double)samples * daysPerYear,
                daysPerYear);
            if (MeteorShowerActivity.GetSeasonalFactor(shower, solarLongitudeDeg) > 0.0)
            {
                active++;
            }
        }

        return active / (double)samples;
    }

    private static double MeasureActiveSpanDegrees(MeteorShowerEntry shower)
    {
        const int samples = 360000;
        const double step = 360.0 / samples;
        var active = 0;

        for (var sample = 0; sample < samples; sample++)
        {
            if (MeteorShowerActivity.GetSeasonalFactor(shower, sample * step) > 0.0)
            {
                active++;
            }
        }

        return active * step;
    }

    /// <summary>0 spring, 1 summer, 2 autumn, 3 winter, counted from the March equinox.</summary>
    private static int SeasonIndex(double yearFraction) => (int)Math.Floor(yearFraction * 4.0) % 4;
}
