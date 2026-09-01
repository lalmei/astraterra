using Xunit;

namespace AstraTerra.Tests.Client.Rendering;

public sealed class SextantObservationWiringTests
{
    [Fact]
    public void Sextant_Sends_Universal_Hour_And_Observer_Longitude_As_Separate_Fields()
    {
        var source = ReadSource();
        var send = source[source.IndexOf("bookClient.SendRecordObservation", StringComparison.Ordinal)..];
        send = send[..send.IndexOf(");", StringComparison.Ordinal)];

        Assert.Contains("calendar.HourOfDay", send);
        Assert.Contains("siderealAngle", send);
        Assert.Contains("longitude", send);
    }

    private static string ReadSource()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AstraTerra.sln")))
        {
            directory = directory.Parent;
        }

        var root = directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate repository root.");
        return File.ReadAllText(Path.Combine(root, "src", "AstraTerra", "Client", "Rendering", "SextantReadingRenderer.cs"));
    }
}
