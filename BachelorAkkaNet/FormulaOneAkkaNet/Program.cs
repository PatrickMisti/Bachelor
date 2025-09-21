using FormulaOneAkkaNet.Config;


string defaultUrl = "https://api.openf1.org";

var builder = Host.CreateApplicationBuilder(args);

var akkaHc = new AkkaConfig
{
    Port = PortChecker.CheckPort(5000),
    Role = "backend"
};

builder.CreateLoggingAdapter();

builder.Services
    .AddHttpService(defaultUrl)
    .UseAkka(akkaHc);

var app = builder.Build();
app.Run();