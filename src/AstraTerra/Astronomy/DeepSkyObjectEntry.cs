namespace AstraTerra.Astronomy;

public sealed record DeepSkyObjectEntry(
    string Id,
    string DisplayName,
    double RightAscensionDeg,
    double DeclinationDeg,
    double AngularSizeDeg,
    IReadOnlyList<EquatorialCoordinates> WorldCoords,
    double Brightness,
    float TintR,
    float TintG,
    float TintB,
    string TexturePath,
    IReadOnlyList<string> FallbackTexturePaths
);
