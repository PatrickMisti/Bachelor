namespace Infrastructure.General;

public class AkkaHostingConfig
{
    // Default hostname is localhost
    public string Hostname { get; set; } = "localhost";
    // Default port is 0, which means a random port will be assigned
    public int Port { get; set; } = 0;
    // Default cluster name
    public string ClusterName { get; set; } = "cluster-system";
    // List of seed nodes for the cluster
    public string[] SeedNodes { get; set; } = [ 
        "akka.tcp://cluster-system@localhost:5000", 
        //"akka.tcp://cluster-system@localhost:6000"
    ];
    // Roles for the cluster nodes
    public List<string>? Roles { get; set; }
    // Default role for the cluster nodes
    public string Role { get; set; } = string.Empty;
    // Shard name for the cluster
    public string ShardName { get; set; } = "driver";
}