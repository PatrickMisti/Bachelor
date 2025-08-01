using Akka.Configuration;

namespace Infrastructure.Cluster.Config;

public static class AkkaConfigLoader
{
    public static async Task<Akka.Configuration.Config> LoadWithPlaceholdersAsync(string filePath, Dictionary<string, string> replacements)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"HOCON file not found at: {filePath}");

        var content = await File.ReadAllTextAsync(filePath);
        foreach (var kvp in replacements)
        {
            content = content.Replace("{{" + kvp.Key + "}}", kvp.Value);
        }

        return ConfigurationFactory.ParseString(content);
    }

    public static async Task<Akka.Configuration.Config> LoadAsync(
        string filePath,
        int port,
        int? seedPort,
        string clusterName)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"HOCON file not found at: {filePath}");

        var template = await File.ReadAllTextAsync(filePath);
        seedPort ??= port;

        var configString = template
            .Replace("{{PORT}}", port.ToString())
            .Replace("{{SEED_PORT}}", seedPort.ToString())
            .Replace("{{CLUSTER_NAME}}", clusterName);

        return ConfigurationFactory.ParseString(configString);
    }
}