using AstraTerra.Astronomy;
using AstraTerra.Constellations;
using AstraTerra.Observation;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace AstraTerra.Client.Rendering;

public sealed class AstrolabePlannerRenderer : IRenderer
{
    private const float TitleTopOffset = 48.0f;
    private const float ReadingTopOffset = 86.0f;
    private const float ClockTopOffset = 118.0f;
    private const float HelpTopOffset = 150.0f;

    /// <summary>How far the player must travel before the sky clock is worth recomputing.</summary>
    private const double ClockCacheBlockSize = 32.0;

    private readonly ICoreClientAPI api;
    private readonly StarCatalog catalog;
    private readonly ConstellationBookClient bookClient;
    private LoadedTexture? titleTexture;
    private LoadedTexture? readingTexture;
    private LoadedTexture? clockTexture;
    private LoadedTexture? helpTexture;
    private string? titleText;
    private string? readingText;
    private string? clockText;
    private string? helpText;
    private string? cachedClockLine;
    private long cachedClockMinute;
    private int cachedClockPositionKey;
    private bool renderingDisabledAfterFailure;

    public AstrolabePlannerRenderer(
        ICoreClientAPI api,
        StarCatalog catalog,
        ConstellationBookClient bookClient)
    {
        this.api = api;
        this.catalog = catalog;
        this.bookClient = bookClient;
    }

    public double RenderOrder => 0.99;

    public int RenderRange => 9999;

    public void OnMouseDown(MouseEvent args)
    {
        if (!IsReadingAstrolabe() || args.Button != EnumMouseButton.Middle)
        {
            return;
        }

        AstrolabeReadingState.CycleTarget(GetCurrentTargets().Count);
        args.Handled = true;
    }

    public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
    {
        if (stage != EnumRenderStage.Ortho || renderingDisabledAfterFailure || !IsReadingAstrolabe())
        {
            return;
        }

        try
        {
            RenderPlanner();
        }
        catch (Exception exception)
        {
            renderingDisabledAfterFailure = true;
            api.Logger.Warning("AstraTerra disabled astrolabe planning HUD after an unexpected error: {0}", exception);
        }
    }

    public void Dispose()
    {
        DeleteTexture(titleTexture);
        DeleteTexture(readingTexture);
        DeleteTexture(clockTexture);
        DeleteTexture(helpTexture);
    }

    private void RenderPlanner()
    {
        var journal = bookClient.ReadCurrentJournal();
        if (journal is null)
        {
            RenderLines(
                "Calibrated Astrolabe",
                "Hold a written constellation book in your left hand.",
                ReadSkyClockLine(api.World.Calendar.TotalDays),
                string.Empty);
            return;
        }

        var targets = AstrolabeService.BuildTargets(journal, catalog);
        if (targets.Count == 0)
        {
            RenderLines(
                "Calibrated Astrolabe",
                "No readable constellations are recorded in this book.",
                ReadSkyClockLine(api.World.Calendar.TotalDays),
                string.Empty);
            return;
        }

        var calendar = api.World.Calendar;
        var hoursPerDay = Math.Max(1.0, calendar.HoursPerDay);
        var daysPerYear = Math.Max(1, calendar.DaysPerYear);
        var forecastTotalDays = calendar.TotalDays + (AstrolabeReadingState.ForecastHours / hoursPerDay);
        var position = api.World.Player.Entity.Pos;
        var latitude = LatitudeMapper.MapGameLatitude(
            position.Z,
            calendar.OnGetLatitude is null ? null : z => calendar.OnGetLatitude(z));
        var longitude = LatitudeMapper.MapWorldLongitude(
            position.X,
            api.World.BlockAccessor.MapSizeX,
            api.World.BlockAccessor.MapSizeZ);
        var selectedIndex = AstrolabeReadingState.NormalizeTargetIndex(targets.Count);
        var reading = AstrolabeService.Read(
            targets[selectedIndex],
            latitude,
            forecastTotalDays,
            daysPerYear,
            hoursPerDay,
            longitude);

        var forecastDay = PositiveModulo((int)Math.Floor(forecastTotalDays), daysPerYear) + 1;
        var title = $"Calibrated Astrolabe — {FormatForecastOffset(AstrolabeReadingState.ForecastHours, hoursPerDay)} — {FormatLatitude(latitude)} — day {forecastDay}/{daysPerYear}";
        var details = $"{reading.Target.DisplayName} ({selectedIndex + 1}/{targets.Count}) — {FormatReading(reading)}";
        var help = "Middle click: next constellation   Scroll: 1 hour   Sneak + scroll: 7 days";
        RenderLines(title, details, ReadSkyClockLine(forecastTotalDays), help);
    }

    /// <summary>
    /// The astrolabe's other half: reading the hour off the sky. The sun altitude comes from
    /// Vintage Story so the hour lines up with the daylight the player can see, and it is sampled
    /// at the forecast time so scrolling ahead moves the clock with the stars.
    /// </summary>
    private string ReadSkyClockLine(double totalDays)
    {
        var calendar = api.World.Calendar;
        var hoursPerDay = Math.Max(1.0, calendar.HoursPerDay);
        var position = api.World.Player.Entity.Pos.XYZ;

        // Finding the next sunrise and sunset costs a few hundred sun samples, but the line only
        // changes once a world minute, and this runs every frame while the astrolabe is raised.
        // Recompute on the minute, or once the player has travelled far enough to move the sun.
        var minute = (long)Math.Floor(totalDays * hoursPerDay * 60.0);
        var positionKey = HashCode.Combine(
            (long)Math.Floor(position.X / ClockCacheBlockSize),
            (long)Math.Floor(position.Z / ClockCacheBlockSize));
        if (cachedClockLine is not null && minute == cachedClockMinute && positionKey == cachedClockPositionKey)
        {
            return cachedClockLine;
        }

        var clock = SkyClock.Read(totalDays, hoursPerDay, days => SunAltitudeDegAt(position, days));
        cachedClockLine = FormatSkyClock(clock, hoursPerDay);
        cachedClockMinute = minute;
        cachedClockPositionKey = positionKey;
        return cachedClockLine;
    }

    private double SunAltitudeDegAt(Vec3d position, double totalDays)
    {
        var vector = api.World.Calendar.GetSunPosition(position, totalDays);
        return SkyBodyModel.FromWorldDirection("Sun", vector.X, vector.Y, vector.Z)?.AltitudeDeg ?? 0.0;
    }

    private static string FormatSkyClock(SkyClockReading clock, double hoursPerDay)
    {
        var phase = clock.Phase switch
        {
            SkyPhase.Day => "daylight",
            SkyPhase.Dusk => "dusk",
            SkyPhase.Night => "night",
            SkyPhase.Dawn => "dawn",
            _ => "unknown"
        };

        var next = clock.Phase == SkyPhase.Day
            ? clock.HoursUntilSunset is { } sunset
                ? $"sunset in {sunset:0.0} h"
                : "the sun does not set"
            : clock.HoursUntilSunrise is { } sunrise
                ? $"sunrise in {sunrise:0.0} h"
                : "the sun does not rise";

        return $"{FormatClockTime(clock.LocalTimeHours, hoursPerDay)} — {phase}, sun {clock.SunAltitudeDeg:+0.0;-0.0;0.0}°, {next}";
    }

    private static string FormatClockTime(double localTimeHours, double hoursPerDay)
    {
        var wrapped = Math.Clamp(localTimeHours, 0.0, hoursPerDay);
        var hours = (int)Math.Floor(wrapped);
        var minutes = (int)Math.Floor((wrapped - hours) * 60.0);
        if (minutes >= 60)
        {
            minutes = 59;
        }

        return $"{hours:00}:{minutes:00}";
    }

    private IReadOnlyList<AstrolabeTarget> GetCurrentTargets()
    {
        var journal = bookClient.ReadCurrentJournal();
        return journal is null ? [] : AstrolabeService.BuildTargets(journal, catalog);
    }

    private bool IsReadingAstrolabe()
    {
        var stack = api.World.Player.InventoryManager.ActiveHotbarSlot?.Itemstack;
        var code = stack?.Collectible?.Code;
        var isHeldAstrolabe = string.Equals(code?.Domain, "astraterra", StringComparison.OrdinalIgnoreCase)
            && string.Equals(code?.Path, "calibrated-astrolabe", StringComparison.OrdinalIgnoreCase);
        return isHeldAstrolabe &&
               (AstrolabeReadingState.IsReading || api.World.Player.Entity.Controls.RightMouseDown);
    }

    private void RenderLines(string title, string reading, string clock, string help)
    {
        RenderLine(
            ref titleTexture,
            ref titleText,
            title,
            new CairoFont(20, GuiStyle.StandardFontName, ColorUtil.WhiteArgbDouble, ColorUtil.BlackArgbDouble),
            TitleTopOffset);
        RenderLine(
            ref readingTexture,
            ref readingText,
            reading,
            new CairoFont(22, GuiStyle.StandardFontName, ColorUtil.WhiteArgbDouble, ColorUtil.BlackArgbDouble),
            ReadingTopOffset);
        RenderLine(
            ref clockTexture,
            ref clockText,
            clock,
            new CairoFont(18, GuiStyle.StandardFontName, ColorUtil.WhiteArgbDouble, ColorUtil.BlackArgbDouble),
            ClockTopOffset);
        RenderLine(
            ref helpTexture,
            ref helpText,
            help,
            new CairoFont(15, GuiStyle.StandardFontName, ColorUtil.WhiteArgbDouble, ColorUtil.BlackArgbDouble),
            HelpTopOffset);
    }

    private void RenderLine(
        ref LoadedTexture? texture,
        ref string? cachedText,
        string text,
        CairoFont font,
        float topOffset)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            DeleteTexture(texture);
            texture = null;
            cachedText = null;
            return;
        }

        if (texture is null || cachedText != text)
        {
            DeleteTexture(texture);
            cachedText = text;
            texture = api.Gui.TextTexture.GenTextTexture(text, font, null);
        }

        if (texture.TextureId == 0)
        {
            return;
        }

        var x = (api.Render.FrameWidth - texture.Width) / 2f;
        api.Render.Render2DLoadedTexture(texture, x, topOffset, 1001f);
    }

    private void DeleteTexture(LoadedTexture? texture)
    {
        if (texture is null)
        {
            return;
        }

        if (texture.TextureId != 0)
        {
            try
            {
                api.Render.GLDeleteTexture(texture.TextureId);
            }
            catch (Exception exception)
            {
                api.Logger.Warning("AstraTerra could not delete astrolabe HUD texture during cleanup: {0}", exception.Message);
            }
        }

        texture.TextureId = 0;
    }

    private static string FormatReading(AstrolabeReading reading)
    {
        var state = reading.MotionState switch
        {
            AstrolabeMotionState.Rising => "rising",
            AstrolabeMotionState.Culminating => "culminating",
            AstrolabeMotionState.Setting => "setting",
            AstrolabeMotionState.BelowHorizon => "below horizon",
            AstrolabeMotionState.NeverRises => "never rises",
            _ => "unknown"
        };
        var horizon = reading.HorizonClass == AstrolabeHorizonClass.Circumpolar
            ? ", circumpolar"
            : string.Empty;
        var direction = AstrolabeService.CompassPoint(reading.AzimuthDeg);
        var altitude = reading.AltitudeDeg.ToString("+0.0;-0.0;0.0");
        var transit = reading.MotionState == AstrolabeMotionState.NeverRises
            ? string.Empty
            : $", {FormatTransit(reading.HoursUntilTransit)}";
        return $"{direction}, {altitude}°, {state}{horizon}{transit}";
    }

    private static string FormatTransit(double hoursUntilTransit)
        => hoursUntilTransit < 0.05
            ? "transiting now"
            : $"transit in {hoursUntilTransit:0.0} h";

    private static string FormatLatitude(double latitudeDeg)
    {
        if (Math.Abs(latitudeDeg) < 0.05)
        {
            return "latitude 0.0°";
        }

        return $"latitude {Math.Abs(latitudeDeg):0.0}° {(latitudeDeg > 0 ? "N" : "S")}";
    }

    private static string FormatForecastOffset(double forecastHours, double hoursPerDay)
    {
        if (forecastHours < 0.05)
        {
            return "Now";
        }

        var days = (int)Math.Floor(forecastHours / hoursPerDay);
        var hours = forecastHours - (days * hoursPerDay);
        if (days == 0)
        {
            return $"+{hours:0} h";
        }

        return hours < 0.05
            ? $"+{days} d"
            : $"+{days} d {hours:0} h";
    }

    private static int PositiveModulo(int value, int modulus)
    {
        var wrapped = value % modulus;
        return wrapped < 0 ? wrapped + modulus : wrapped;
    }
}
