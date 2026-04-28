using System.Text.Json;
using AspirePoc.Amp;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var fixturePath = Path.Combine(AppContext.BaseDirectory, "customers.json");
var fixtureJson = await File.ReadAllTextAsync(fixturePath);
var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
var customers = JsonSerializer.Deserialize<Customer[]>(fixtureJson, jsonOptions)
    ?? throw new InvalidOperationException($"Customers fixture at {fixturePath} is empty or invalid");
var customersById = customers.ToDictionary(c => c.Id);

var app = builder.Build();

app.MapDefaultEndpoints();

app.MapGet("/customers/{id}", (string id) =>
    customersById.TryGetValue(id, out var customer)
        ? Results.Ok(customer)
        : Results.NotFound());

app.Run();
