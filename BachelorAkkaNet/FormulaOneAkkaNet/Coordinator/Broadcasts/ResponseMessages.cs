namespace FormulaOneAkkaNet.Coordinator.Broadcasts;

public record IngressActivateResponse(bool CanBeActivated);

public record IngressCountResponse(int ActiveIngress);

public record ShardCountResponse(int ActiveShards);
