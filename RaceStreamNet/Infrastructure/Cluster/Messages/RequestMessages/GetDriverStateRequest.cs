namespace Infrastructure.Cluster.Messages.RequestMessages;

public sealed class GetDriverStateRequest
{
    public string Id { get; set; } = string.Empty;

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