using Akka.Actor;
using Akka.Cluster;
using Akka.Cluster.Sharding;
using Infrastructure.Testing;
using SystemTests;
using Akka.Remote;                         // <— wichtig
using Akka.Cluster.Tools.PublishSubscribe;
using Akka.Configuration;
using Infrastructure.General.PubSub;
using Infrastructure.Models;
using Infrastructure.Shard.Messages;

var driverId = "VER"; // ID kann per Argument übergeben werden
var host = "localhost"; // exakt wie in deinen SeedNodes
var port = 0; // 0 = freier Port

var config = Akka.Configuration.ConfigurationFactory
    .ParseString(Hocon.Build(host, port))
    .WithFallback(DistributedPubSub.DefaultConfig())
    .WithFallback(ConfigurationFactory.Default());

// WICHTIG: Systemname identisch zum Service
using var system = ActorSystem.Create("cluster-system", config);

try
{
    // 1) Cluster Join abwarten (Service + wir selbst müssen Up sein)
    var cluster = Cluster.Get(system);
    await MemberUpUtilities.WaitForMemberUp(system, TimeSpan.FromSeconds(30), cluster);


    // 2) Mediator initialisieren und kurz warten bis SubscribeAcks durch sind
    DistributedPubSub.Get(system);

    /*var proxyRegion = await ClusterSharding.Get(system).StartProxyAsync(
        typeName: "driver",
        role: null!,
        messageExtractor: new DriverMessageExtractorTest());

    await Task.Delay(1000);

    proxyRegion.Tell(new UpdateDriverTelemetry(
        DriverId: "VER",
        LapNumber: 12,
        PositionOnTrack: 3,
        Speed: 278.5,
        DeltaToLeader: 1.234,
        TyreLife: 10,
        CurrentTyreCompound: TyreCompound.Soft,
        PitStops: 1,
        LastLapTime: TimeSpan.FromSeconds(92.345),
        Sector1Time: TimeSpan.FromSeconds(29.1),
        Sector2Time: TimeSpan.FromSeconds(31.2),
        Sector3Time: TimeSpan.FromSeconds(32.0),
        Timestamp: DateTime.UtcNow));
*/
    await Task.Delay(1000);

    // 3) Client-Actor starten, Publish senden, Antwort abwarten
    var client = system.ActorOf(Props.Create(() => new PubSubClientActor(driverId)), "pubsub-client");

    // 4) Einfach laufen lassen, bis Actor sich beendet
    var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
    await system.WhenTerminated;//ContinueWith(_ => { }, cts.Token);

    return 0;
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("[ERR] Timed out waiting for response.");
    return 3;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.ToString());
    return 1;
}
finally
{
    //await system.Terminate();
}

