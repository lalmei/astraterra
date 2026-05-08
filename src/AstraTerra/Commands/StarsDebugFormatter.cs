namespace AstraTerra.Commands;

public static class StarsDebugFormatter
{
    public static string FormatDisplayName(string? name, int id)
        => string.IsNullOrWhiteSpace(name) ? $"Unnamed Constellation (#{id})" : $"{name} (#{id})";

    public static string FormatDebug(
        double latitudeDeg,
        int latitudeWrapCycle,
        double siderealAngleDeg,
        string gatingReason,
        int? hoveredGuideHipId = null,
        int? selectedConstellationId = null,
        bool guideStarEmphasis = false)
    {
        return $"lat={latitudeDeg:0.###}; wrap={latitudeWrapCycle}; sidereal={siderealAngleDeg:0.###}; gate={gatingReason}; " +
               $"hovered={FormatNullable(hoveredGuideHipId)}; selected={FormatNullable(selectedConstellationId)}; guideEmphasis={guideStarEmphasis.ToString().ToLowerInvariant()}";
    }

    private static string FormatNullable(int? value) => value?.ToString() ?? "none";
}
