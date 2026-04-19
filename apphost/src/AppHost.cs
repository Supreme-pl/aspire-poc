var builder = DistributedApplication.CreateBuilder(args);

var app1 = builder.AddProject<Projects.AspirePoc_App1>("app1");
var app2 = builder.AddProject<Projects.AspirePoc_App2>("app2");

builder.Build().Run();
