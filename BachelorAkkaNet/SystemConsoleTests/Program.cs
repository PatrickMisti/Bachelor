using Akka.Actor;
using Akka.Cluster;
using Akka.Cluster.Tools.PublishSubscribe;
using Akka.Configuration;
using Akka.Serialization;
using Infrastructure.PubSub;
using Infrastructure.PubSub.Messages;
using SystemConsoleTests;


string host = "localhost";
var port = 0;

var config = ConfigurationFactory
    .ParseString(HoconGen.Build(host, port))
    .WithFallback(DistributedPubSub.DefaultConfig())
    .WithFallback(ConfigurationFactory.Default())
    .WithFallback(HyperionSerializer.DefaultConfiguration());

using var system = ActorSystem.Create("cluster-system", config);

var actor = system.ActorOf(ClientApiActor.Prop());
var cluster = Cluster.Get(system);
await MemberEventUp.WaitForMemberUp(system, TimeSpan.FromSeconds(30), cluster);

DistributedPubSub.Get(system);
await Task.Delay(1000);

system.PubSub().Ingress.Publish(new IngressSessionRaceMessage(9158));

await Task.Delay(Timeout.Infinite);
