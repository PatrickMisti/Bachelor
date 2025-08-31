using Akka.Actor;
using Infrastructure.General.Message;
using Newtonsoft.Json;

namespace Infrastructure.Shard.Messages.RequestMessages;

public sealed class GetDriverStateRequest : IPubMessage
{
    public string Id { get; set; } = string.Empty;

    [JsonConstructor]
    public GetDriverStateRequest()
    { }

    
    public GetDriverStateRequest(string id)
    {
        Id = id;
    }

    public override string ToString()
    {
        return $"GetDriverStateRequest: {Id}";
    }
}