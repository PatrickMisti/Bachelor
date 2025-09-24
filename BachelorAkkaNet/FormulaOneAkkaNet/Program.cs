using FormulaOneAkkaNet.Config;
using Infrastructure.General;


string defaultUrl = "https://api.openf1.org";
string defaultNodeName = "backend";
int defaultPort = 7000;

var builder = Host.CreateApplicationBuilder(args);

var rolesArg = args.Length > 0 ? args[0] : null;
var roles = rolesArg?
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList()
            ??
            [
                ClusterMemberRoles.Controller.ToStr(),
                ClusterMemberRoles.Backend.ToStr(),
                ClusterMemberRoles.Ingress.ToStr(),
                ClusterMemberRoles.Api.ToStr()
            ];

if (roles.Contains(ClusterMemberRoles.Controller.ToStr()) && roles.Count == 1)
    defaultPort = 6000;
else if (roles.Contains(ClusterMemberRoles.Backend.ToStr()) && roles.Count == 1)
    defaultPort = 5000;
else if (roles.Count > 1)
    defaultPort = 5000;

    var akkaHc = new AkkaConfig
    {
        Port = PortChecker.CheckPort(defaultPort),
        Role = roles.Count == 1 ? roles.First() : defaultNodeName,
        Roles = roles
    };

builder.CreateLoggingAdapter();

builder.Services
    .AddHttpService(defaultUrl)
    .UseAkka(akkaHc);

var app = builder.Build();
app.Run();