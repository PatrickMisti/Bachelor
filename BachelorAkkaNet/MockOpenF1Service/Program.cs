using System.Text.Json.Serialization;
using MockOpenF1Service.Services;
using MockOpenF1Service.Utilities;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Logging.ClearProviders();
builder.Logging.AddSerilog(Log.Logger);


builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddSwaggerGen();

builder.Services.AddSignalR()
    .AddJsonProtocol(opt =>
    {
        opt.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        opt.PayloadSerializerOptions.PropertyNameCaseInsensitive = true;
    })
    .AddMessagePackProtocol();

builder.Services
    .AddHttpClient<HttpClientWrapper>(
        opt => opt.BaseAddress = new Uri(HttpClientWrapper.ApiBaseUrlConfig));

builder.Services.AddScoped<DriverService>();




var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app
        .UseSwagger()
        .UseSwaggerUI();
    
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseAuthorization();

app.MapControllers();


app.MapHub<DriverForSessionHub>(ConfigWrapper.DriverInfoHubName);
//app.MapHub<RaceSessionService>(ConfigWrapper.RaceSessionHubName);
app.Run();
