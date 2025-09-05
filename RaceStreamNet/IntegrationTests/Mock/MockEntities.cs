using Infrastructure.Shard.Messages;
using Infrastructure.Shard.Models;

namespace IntegrationTests.Mock;

public static class MockEntities
{
    public static CreateModelDriverMessage MOCK_UPDATE_DRIVER_TELEMETRY => new CreateModelDriverMessage(DriverKey.Create(1111, 1))
    {
        FirstName = "Max",
        LastName = "Herbert",
        Acronym = "HSV",
        CountryCode = "AT",
        TeamName = "Red Bull"
    };

}

