namespace AstraTerra.Astronomy;

public sealed record DeepSkyObjectEntry(
    string Id,
    string DisplayName,
    double RightAscensionDeg,
    double DeclinationDeg,
    double AngularSizeDeg,
    double Brightness,
    float TintR,
    float TintG,
    float TintB,
    string TexturePath,
    IReadOnlyList<string> FallbackTexturePaths
);
