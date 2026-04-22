var builder = DistributedApplication.CreateBuilder(args);

var cache = builder.AddRedis("cache")
    .WithRedisInsight();

var kafka = builder.AddContainer("redpanda", "redpandadata/redpanda", "v24.2.4")
    .WithArgs(
        "redpanda", "start",
        "--smp", "1",
        "--overprovisioned",
        "--kafka-addr", "internal://0.0.0.0:9093,external://0.0.0.0:9092",
        "--advertise-kafka-addr", "internal://redpanda:9093,external://localhost:9092")
    .WithEndpoint(port: 9092, targetPort: 9092, name: "kafka-external");

var kafkaConsole = builder.AddContainer("redpanda-console", "redpandadata/console", "v2.7.0")
    .WithEnvironment("KAFKA_BROKERS", "redpanda:9093")
    .WithHttpEndpoint(targetPort: 8080, name: "http")
    .WaitFor(kafka);

var referenceService = builder.AddProject<Projects.AspirePoc_ReferenceService>("reference-service");

var app1 = builder.AddProject<Projects.AspirePoc_App1>("app1")
    .WithReference(cache)
    .WithReference(referenceService)
    .WaitFor(cache)
    .WaitFor(kafka)
    .WaitFor(referenceService)
    .WithEnvironment("Kafka__BootstrapServers", "localhost:9092");

var app2 = builder.AddProject<Projects.AspirePoc_App2>("app2")
    .WaitFor(kafka)
    .WithEnvironment("Kafka__BootstrapServers", "localhost:9092");

var producer = builder.AddProject<Projects.AspirePoc_Producer>("producer")
    .WithReference(app1)
    .WaitFor(app1);

builder.Build().Run();
