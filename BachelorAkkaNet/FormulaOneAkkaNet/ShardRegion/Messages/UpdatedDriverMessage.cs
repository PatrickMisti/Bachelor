using Infrastructure.ShardRegion;

namespace FormulaOneAkkaNet.ShardRegion.Messages;

public sealed class UpdatedDriverMessage(DriverKey key, DriverStateDto? state) : IHasDriverId
{
    public DriverKey Key { get; set; } = key;

    public DriverStateDto? State { get; set; } = state;
}
