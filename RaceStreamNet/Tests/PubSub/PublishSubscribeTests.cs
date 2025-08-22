using Akka.Actor;
using Akka.Cluster;
using Akka.Cluster.Tools.PublishSubscribe;
using Akka.Configuration;
using Akka.TestKit.Xunit2;
using Infrastructure.General.PubSub;
using Infrastructure.Testing;
using Xunit;

namespace Tests.PubSub;

public class PublishSubscribeTests() : TestKit(TestConfig)
{
    private static readonly Config TestConfig =
        ConfigurationFactory.ParseString("""
            
                                         akka {
                                             actor.provider = cluster
                                             remote.dot-netty.tcp.hostname = ""127.0.0.1""
                                             remote.dot-netty.tcp.port = 0
                                             loglevel = INFO
                                             log-dead-letters-during-shutdown = off
                                             remote.log-serialization-warnings = on
                                         }
            
            """).WithFallback(DistributedPubSub.DefaultConfig());

    [Fact]
    public async Task Should_publish_and_receive_on_topic()
    {
        // 1) Single-node-Cluster up
        var cluster = Cluster.Get(Sys);
        cluster.Join(cluster.SelfAddress);
        await MemberUpUtilities.WaitForMemberUp(Sys, TimeSpan.FromSeconds(5), Cluster.Get(Sys));

        // 2) Mediator grab
        var mediator = DistributedPubSub.Get(Sys).Mediator;

        // 3) Subscribe to Test_Actor topic
        mediator.Tell(new Subscribe("drivers", TestActor));
        ExpectMsg<SubscribeAck>(TimeSpan.FromSeconds(3));

        // 4) Publish
        mediator.Tell(new Publish("drivers", "hello-driver"));

        // 5) Get
        ExpectMsg<string>(msg => msg == "hello-driver", TimeSpan.FromSeconds(3));
    }
    
    [Fact]
    public async Task Should_subscribe_and_receive_messages_via_PubSub_fluent_API()
    {
        var cluster = Cluster.Get(Sys);
        await cluster.JoinAsync(cluster.SelfAddress);
        await MemberUpUtilities.WaitForMemberUp(Sys, TimeSpan.FromSeconds(5), cluster);

        var probe = CreateTestProbe();

        Sys.ActorOf(Props.Create(() => new AllSubscriberTest(probe.Ref)), "all-sub");
        Sys.ActorOf(Props.Create(() => new BackendSubscriberTest(probe.Ref)), "backend-sub");

        Sys.PubSub().Backend.Publish(new MessageTest{Message = "hello-backend" });
        Sys.PubSub().All.Publish(new MessageTest { Message = "broadcast-all" });

        var m1 = probe.ExpectMsg<(string kind, object msg)>(TimeSpan.FromSeconds(3));
        var m2 = probe.ExpectMsg<(string kind, object msg)>(TimeSpan.FromSeconds(3));
        var m3 = probe.ExpectMsg<(string kind, object msg)>(TimeSpan.FromSeconds(3));

        string[] payload = [m1.kind, m2.kind, m3.kind];
        int expectedBack = payload.Count(element => element.Contains("msg back"));
        int expectedAll = payload.Count(element => element.Contains("msg all"));

        Assert.Equal(2, expectedBack);
        Assert.Equal(1, expectedAll);
    }
}