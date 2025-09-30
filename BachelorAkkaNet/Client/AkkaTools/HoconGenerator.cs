namespace Client.AkkaTools;

internal static class HoconGenerator
{
    public static string Hocon => """
                                  akka {
                                    loglevel = "INFO"
                                    stdout-loglevel = "OFF"
                                    loggers = ["Akka.Logger.Serilog.SerilogLogger, Akka.Logger.Serilog"]
                                  
                                    actor {
                                      provider = cluster
                                      serializers {
                                        hyperion = "Akka.Serialization.HyperionSerializer, Akka.Serialization.Hyperion"
                                      }
                                      serialization-bindings {
                                        "System.Object" = hyperion
                                      }
                                    }
                                  
                                    remote.dot-netty.tcp { 
                                        hostname = "localhost", 
                                        port = 0 
                                        
                                        # Standard ist 128000b – hier z.B. 1 MiB
                                        maximum-frame-size = 2MiB
                                  
                                        # optional etwas großzügiger puffern
                                        send-buffer-size    = 2MiB
                                        receive-buffer-size = 2MiB
                                    
                                    }
                                  
                                    cluster {
                                      roles = ["api"]
                                      seed-nodes = [
                                        "akka.tcp://cluster-system@localhost:5000"#,
                                        #"akka.tcp://cluster-system@localhost:6000"
                                      ]
                                    }
                                  }
                                  
                                  """;
}