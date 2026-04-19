var builder = DistributedApplication.CreateBuilder(args);

var cache = builder.AddRedis("cache");

var kafka = builder.AddContainer("redpanda", "redpandadata/redpanda", "v24.2.4")
    .WithArgs(
        "redpanda", "start",
        "--smp", "1",
        "--overprovisioned",
        "--kafka-addr", "0.0.0.0:9092",
        "--advertise-kafka-addr", "localhost:9092")
    .WithEndpoint(port: 9092, targetPort: 9092, name: "kafka");

var app1 = builder.AddProject<Projects.AspirePoc_App1>("app1")
    .WithReference(cache)
    .WaitFor(cache)
    .WithEnvironment("Kafka__BootstrapServers", "localhost:9092");

var app2 = builder.AddProject<Projects.AspirePoc_App2>("app2")
    .WithEnvironment("Kafka__BootstrapServers", "localhost:9092");

builder.Build().Run();
