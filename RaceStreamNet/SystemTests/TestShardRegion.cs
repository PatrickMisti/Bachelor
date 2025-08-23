using Akka.Actor;
using Akka.Cluster;
using Infrastructure.Testing;
using SystemTests;
using Akka.Remote;                         // <— wichtig
using Akka.Cluster.Tools.PublishSubscribe;
using Akka.Configuration;

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

