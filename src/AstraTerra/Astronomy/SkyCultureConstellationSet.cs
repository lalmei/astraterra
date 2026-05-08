namespace AstraTerra.Astronomy;

public sealed record SkyCultureConstellationSet(
    int SchemaVersion,
    string Id,
    string DisplayName,
    IReadOnlyList<string> Classification,
    SkyCultureSource Source,
    IReadOnlyList<SkyCultureConstellation> Constellations
);

public sealed record SkyCultureSource(
    string Name,
    string Url,
    string License,
    string Notes
);

public sealed record SkyCultureConstellation(
    string IauCode,
    string DisplayName,
    IReadOnlyList<IReadOnlyList<int>> Lines
);
