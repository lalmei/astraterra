namespace AstraTerra.Astronomy;

public sealed record GuideStarGroup(
    string IauCode,
    string DisplayName,
    IReadOnlyList<int> HipIds
);
