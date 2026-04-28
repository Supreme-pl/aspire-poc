using AspirePoc.Gch;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddHttpClient(Constants.PreProcessorClientName, client =>
{
    client.BaseAddress = new Uri("http://pre-processor");
});

builder.Services.AddSingleton<BatchGenerator>();
builder.Services.AddHostedService<ProducerService>();

var host = builder.Build();
host.Run();
