namespace AstraTerra.Astronomy;

public sealed record StarCatalogEntry(
    int Hip,
    double RightAscensionDeg,
    double DeclinationDeg,
    double VisualMagnitude,
    double? BvColorIndex,
    bool IsGuideStar
);
