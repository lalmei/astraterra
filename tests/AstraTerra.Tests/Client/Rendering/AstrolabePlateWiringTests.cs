using Xunit;

namespace AstraTerra.Tests.Client.Rendering;

/// <summary>
/// The instrument's behaviour depends on two things no unit test can reach, because both live
/// inside Vintage Story types: the HUD reading its latitude off the plate rather than off the
/// player, and the server being the side that cuts a plate. Both would keep passing every other
/// test in this suite if they regressed — a planner that went back to live latitude would simply be
/// accurate again, silently, and a client-side cut would work perfectly in single player and lose
/// the plate on every dedicated server.
/// </summary>
public sealed class AstrolabePlateWiringTests
{
    [Fact]
    public void The_Planner_Reads_Its_Latitude_From_The_Plate()
    {
        var source = ReadSource("Client", "Rendering", "AstrolabePlannerRenderer.cs");

        Assert.Contains("AstrolabeCalibrationStore.ReadLatitude", source);
        Assert.Contains("var latitude = plateLatitude.Value;", source);

        // Longitude stays live on purpose: it moves the hour of transit, not the horizon. It is
        // read through ObserverLongitude so the instrument goes back to universal time whenever the
        // visible sun does.
        Assert.Contains("ObserverLongitude.ForObserver", source);
        Assert.DoesNotContain("LatitudeMapper.MapWorldLongitude", source);

        // Nothing between reading the plate and taking the reading may reintroduce the player's own
        // latitude, which is exactly what this feature removed.
        var planner = source[source.IndexOf("var plateLatitude", StringComparison.Ordinal)..];
        planner = planner[..planner.IndexOf("AstrolabeService.Read", StringComparison.Ordinal)];
        Assert.DoesNotContain("MapGameLatitude", planner);
    }

    [Fact]
    public void Forecast_Clock_Uses_The_Forecast_Timestamp_For_Both_Local_And_World_Time()
    {
        var source = ReadSource("Client", "Rendering", "AstrolabePlannerRenderer.cs");

        Assert.Contains("FormatSkyClock(clock, hoursPerDay, totalDays)", source);
        Assert.Contains("GetUniversalSolarTimeHours(totalDays, hoursPerDay)", source);
        Assert.DoesNotContain("GetUniversalSolarTimeHours(calendar.TotalDays", source);
    }

    [Fact]
    public void An_Uncalibrated_Astrolabe_Refuses_To_Place_Anything()
    {
        var source = ReadSource("Client", "Rendering", "AstrolabePlannerRenderer.cs");

        // The no-plate branch has to come before targets are built, or a blank instrument would
        // still answer for wherever the last plate happened to be.
        var noPlate = source.IndexOf("if (plateLatitude is null)", StringComparison.Ordinal);
        var readsBook = source.IndexOf("bookClient.ReadCurrentJournal", StringComparison.Ordinal);
        Assert.InRange(noPlate, 0, readsBook);
    }

    [Fact]
    public void Only_The_Server_Cuts_A_Plate_And_It_Syncs_The_Slot()
    {
        var source = ReadSource("Items", "ItemAstrolabe.cs");

        Assert.Contains("world.Side == EnumAppSide.Server", source);
        Assert.Contains("CutPlate(slot, byEntity, world)", source);

        var cut = source[source.IndexOf("private static void CutPlate", StringComparison.Ordinal)..];
        Assert.Contains("AstrolabeCalibrationStore.Write", cut);

        // Without MarkDirty the plate exists only in the server's copy of the stack.
        Assert.Contains("slot.MarkDirty()", cut);
    }

    /// <summary>
    /// Sneak decides which interaction runs, and it is latched at the start rather than sampled per
    /// step, so releasing sneak mid-sighting cannot hand a part-finished sighting to the planner.
    /// </summary>
    [Fact]
    public void The_Sighting_Latches_Sneak_At_The_Start()
    {
        var source = ReadSource("Items", "ItemAstrolabe.cs");

        var start = source[source.IndexOf("public override void OnHeldInteractStart", StringComparison.Ordinal)..];
        start = start[..start.IndexOf("public override bool OnHeldInteractStep", StringComparison.Ordinal)];
        Assert.Contains("Controls?.Sneak", start);
        Assert.Contains("SetBool(CalibratingAttribute", start);

        var step = source[source.IndexOf("public override bool OnHeldInteractStep", StringComparison.Ordinal)..];
        step = step[..step.IndexOf("public override void OnHeldInteractStop", StringComparison.Ordinal)];
        Assert.Contains("GetBool(CalibratingAttribute)", step);
        Assert.DoesNotContain("Controls", step);
    }

    private static string ReadSource(params string[] relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AstraTerra.sln")))
        {
            directory = directory.Parent;
        }

        var root = directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate repository root.");
        return File.ReadAllText(Path.Combine([root, "src", "AstraTerra", .. relativePath]));
    }
}
