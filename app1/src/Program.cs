var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var app = builder.Build();

app.MapDefaultEndpoints();

app.MapGet("/hello", () => "app1 is alive");

app.Logger.LogInformation(
    "app1 wired with Redis={Redis}, Kafka={Kafka}",
    builder.Configuration.GetConnectionString("cache"),
    builder.Configuration["Kafka:BootstrapServers"]);

app.Run();
