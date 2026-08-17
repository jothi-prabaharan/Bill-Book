using Microsoft.Extensions.Hosting;

IHost host = Host.CreateDefaultBuilder(args)
    .Build();

await host.RunAsync();
