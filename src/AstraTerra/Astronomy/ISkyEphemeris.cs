namespace AstraTerra.Astronomy;

/// <summary>
/// Where a body is, and how bright it is, at a world time.
/// </summary>
/// <remarks>
/// The narrowest contract a planet, a comet and a meteor radiant can all satisfy: two functions of
/// <c>totalDays</c>, and nothing about how the body is drawn, named or catalogued. Implementations
/// belong in <c>Astronomy/</c> and stay pure — plain numbers in, plain numbers out — which is what
/// lets an orbit be checked against published positions for real dates without a running game.
/// <para>
/// Positions are geocentric, so they do not depend on where the observer stands. Latitude enters
/// later, in <see cref="SkyProjection"/>.
/// </para>
/// </remarks>
public interface ISkyEphemeris
{
    /// <param name="totalDays">World time, as <c>IGameCalendar.TotalDays</c> reports it.</param>
    EquatorialCoordinates PositionAt(double totalDays);

    /// <param name="totalDays">World time, as <c>IGameCalendar.TotalDays</c> reports it.</param>
    double MagnitudeAt(double totalDays);
}

/// <summary>
/// A body that does not move: a catalog star, a deep-sky object, a meteor shower radiant.
/// </summary>
/// <remarks>
/// Lets the fixed sky travel the same path as the moving one, so a caller collecting bodies to sight
/// or to forecast does not need to know which kind it is holding.
/// </remarks>
public sealed class FixedEphemeris : ISkyEphemeris
{
    private readonly EquatorialCoordinates coordinates;
    private readonly double visualMagnitude;

    public FixedEphemeris(EquatorialCoordinates coordinates, double visualMagnitude)
    {
        this.coordinates = coordinates;
        this.visualMagnitude = visualMagnitude;
    }

    public EquatorialCoordinates PositionAt(double totalDays) => coordinates;

    public double MagnitudeAt(double totalDays) => visualMagnitude;
}
