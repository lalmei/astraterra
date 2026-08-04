namespace AstraTerra.Astronomy;

public readonly record struct DeepSkyEquatorialCorner(
    double RightAscensionDeg,
    double DeclinationDeg
);

public sealed record DeepSkyObjectEntry(
    string Id,
    string DisplayName,
    double RightAscensionDeg,
    double DeclinationDeg,
    double AngularSizeDeg,
    IReadOnlyList<DeepSkyEquatorialCorner> WorldCoords,
    double Brightness,
    float TintR,
    float TintG,
    float TintB,
    string TexturePath,
    IReadOnlyList<string> FallbackTexturePaths
);
