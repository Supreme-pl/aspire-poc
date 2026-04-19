var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var app = builder.Build();

app.MapDefaultEndpoints();

app.MapGet("/hello", () => "app2 is alive");

app.Logger.LogInformation(
    "app2 wired with Kafka={Kafka}",
    builder.Configuration["Kafka:BootstrapServers"]);

app.Run();
