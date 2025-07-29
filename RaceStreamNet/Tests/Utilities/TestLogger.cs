using Microsoft.Extensions.Logging;
using Serilog;

namespace Tests.Utilities;

public static class TestLogger
{
    private static readonly ILoggerFactory _loggerFactory;

    static TestLogger()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .CreateLogger();

        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddSerilog(Log.Logger, dispose: true);
        });
    }

    public static ILogger<T> CreateLogger<T>() => _loggerFactory.CreateLogger<T>();
}