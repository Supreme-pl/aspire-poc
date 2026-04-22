using AspirePoc.Producer;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddHttpClient("app1", client =>
{
    client.BaseAddress = new Uri("http://app1");
});

builder.Services.AddSingleton<BatchGenerator>();
builder.Services.AddHostedService<ProducerService>();

var host = builder.Build();
host.Run();
