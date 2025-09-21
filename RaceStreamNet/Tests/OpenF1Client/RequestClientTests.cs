using DriverTelemetryIngress.Bridge;
using DriverTelemetryIngress.Config;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace Tests.OpenF1Client;

[TestFixture]
public class RequestClientTests
{
    IHttpWrapperClient _client;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        var host = new ServiceCollection();
        host.ConfigureHttp();
        host.AddLogging(builder => builder.AddConsole());
        var sp = host.BuildServiceProvider();
        _client = sp.GetRequiredService<IHttpWrapperClient>();
    }

    [Test]
    public async Task Test_Get_Driver_Person_Data()
    {
        int sessionKey = 9158;

        var drivers = await _client.GetDriversAsync(sessionKey);

        Assert.That(drivers!.Count() == 20, Is.True);
    }

    [Test]
    public async Task Test_Get_Position_On_Track_Data()
    {
        int session = 9158;

        var positions = await _client.GetPositionsOnTrackAsync(session);

        Assert.That(positions, Is.Not.Empty.Or.Null);
    }

    [Test]
    public async Task Test_Get_Interval_Driver_Data()
    {
        int session = 9165;

        var intervals = await _client.GetIntervalDriversAsync(session);

        Assert.That(intervals, Is.Not.Empty.Or.Null);
    }
}