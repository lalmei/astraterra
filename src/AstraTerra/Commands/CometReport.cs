using System.Globalization;
using AstraTerra.Astronomy;

namespace AstraTerra.Commands;

/// <summary>
/// What every comet is doing right now, for <c>.stars comets</c>.
/// </summary>
/// <remarks>
/// A comet is the one thing in this mod that cannot be checked by looking up. The rarest is due
/// about once a human lifetime of world years, so "is it working?" is otherwise unanswerable without
/// waiting out an apparition — and a bug in the schedule maths would present as a sky that simply
/// never changes. This turns the whole catalog into four lines.
/// </remarks>
public static class CometReport
{
    public static string Describe(CometCatalog? comets, double totalDays, int daysPerYear)
    {
        if (comets is null || comets.Comets.Count == 0)
        {
            return "No comet catalog is loaded.";
        }

        var worldYears = CometApparitionSchedule.WorldYears(totalDays, daysPerYear);
        var lines = comets.Comets.Select(comet => DescribeOne(comet, worldYears, daysPerYear));

        return string.Join(
            "\n",
            new[] { $"Comets at world day {totalDays.ToString("0.0", CultureInfo.InvariantCulture)} (year {worldYears.ToString("0.00", CultureInfo.InvariantCulture)}):" }
                .Concat(lines));
    }

    private static string DescribeOne(CometEntry comet, double worldYears, int daysPerYear)
    {
        var apparition = CometApparitionSchedule.Read(comet, worldYears);
        if (!apparition.IsVisible)
        {
            var opensAt = CometApparitionSchedule.NextWindowOpenYear(comet, worldYears);
            var days = (opensAt - worldYears) * daysPerYear;
            return Format(
                comet,
                $"away; returns in {days.ToString("0", CultureInfo.InvariantCulture)} days " +
                $"(world year {opensAt.ToString("0.00", CultureInfo.InvariantCulture)}, period {comet.PeriodYears.ToString("0.0#", CultureInfo.InvariantCulture)} y)");
        }

        var magnitude = CometApparitionSchedule.GetMagnitude(comet, apparition.Closeness);
        var tail = CometApparitionSchedule.GetTailLengthDeg(comet, apparition.Closeness);
        var position = new CometEphemeris(comet, daysPerYear).PositionAtPhase(apparition.Phase);

        return Format(
            comet,
            $"up; phase {apparition.Phase.ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture)}; " +
            $"magnitude {magnitude.ToString("0.0", CultureInfo.InvariantCulture)}; " +
            $"tail {tail.ToString("0.0", CultureInfo.InvariantCulture)} deg; " +
            $"ra {position.RightAscensionDeg.ToString("0.0", CultureInfo.InvariantCulture)} deg; " +
            $"dec {position.DeclinationDeg.ToString("0.0", CultureInfo.InvariantCulture)} deg");
    }

    private static string Format(CometEntry comet, string state)
        => $"{comet.Id} ({comet.DisplayName}): {state}";
}
