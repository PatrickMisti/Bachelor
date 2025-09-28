using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SystemConsoleTests;

internal class HoconGen
{
    public static string Build(string host, int port) => $@"
akka {{
  loglevel = DEBUG
  stdout-loglevel = DEBUG
  actor.provider = ""cluster""
    serializers {{
      hyperion = ""Akka.Serialization.HyperionSerializer, Akka.Serialization.Hyperion""
    }}
    serialization-bindings {{
      ""System.Object"" = hyperion
    }}

  coordinated-shutdown {{
    run-by-actor-system-terminate = off
    run-by-jvm-shutdown-hook = off
    terminate-actor-system = off
  }}

  remote.dot-netty.tcp {{
    hostname = ""{host}""
    port = {port}
  }}

  cluster {{
    roles = [ ""api"" ]
    # exakt wie dein Service:
    seed-nodes = [
      ""akka.tcp://cluster-system@localhost:5000""
      # ""akka.tcp://cluster-system@localhost:6000""
    ]
    # optional: SBR, falls gewünscht
    # downing-provider-class = ""Akka.Cluster.SplitBrainResolver, Akka.Cluster""
    # split-brain-resolver {{
    #   active-strategy = keep-majority
    #   stable-after = 20s
    # }}
  }}
}}";
}