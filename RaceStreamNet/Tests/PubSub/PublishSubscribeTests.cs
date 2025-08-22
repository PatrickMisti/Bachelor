using Akka.Actor;
using Akka.Cluster;
using Akka.Cluster.Tools.PublishSubscribe;
using Akka.Configuration;
using Akka.TestKit.Xunit2;
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
}