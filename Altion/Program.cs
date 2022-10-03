using Altion;
using Altion.Generate;
using Altion.Sort;
using CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

if (args?.Length < 1)
{
    Console.WriteLine("Use '--help' to see the list of options");
    return -1;
}

var hostBuilder = CreateHostBuilder(args!);
RegisterConfigs(hostBuilder);

await Parser.Default
    .ParseArguments<AltiCommandArgs>(args)
    .WithParsedAsync(async opts => await Run(opts, hostBuilder.Build()));

return 0;

async Task Run(AltiCommandArgs opts, IHost appHost)
{
    if (opts.GenerateTestFile)
    {
        await appHost.Services
            .GetRequiredService<IDataFileGenerator>()
            .Generate();
    }
    else if (opts.Sort)
    {
        await appHost.Services
            .GetRequiredService<ISorter>()
            .Sort();
    }
}

static IHostBuilder CreateHostBuilder(string[] args)
{
    var serviceCandidates = typeof(Program).Assembly.GetTypes()
        .Where(t => !t.IsInterface && !t.IsAbstract)
        .SelectMany(t =>
        {
            var interfaces = t.GetInterfaces();
            return interfaces?.Length < 1 
                ? new[] { new ServiceCandidate { Implementation = t } } 
                : interfaces!.Select(i => new ServiceCandidate { Interface = i, Implementation = t });
        });

    return Host.CreateDefaultBuilder(args)
        .ConfigureServices(s =>
        {
            foreach (var serviceCandidate in serviceCandidates)
            {
                if (serviceCandidate.Interface == null)
                {
                    s.AddTransient(serviceCandidate.Implementation);
                }
                else
                {
                    s.AddTransient(serviceCandidate.Interface, serviceCandidate.Implementation);
                }
            }
        });
}

void RegisterConfigs(IHostBuilder hostBuilder1)
{
    var config = new ConfigurationBuilder()
        .AddJsonFile("appsettings.json")
        .Build()
        .GetRequiredSection("Settings")
        .Get<AltionConfig>();

    hostBuilder1.ConfigureServices(s => s.AddSingleton<IAltionConfig>(config));
}

internal class ServiceCandidate
{
    public Type? Interface { get; init; }
    public Type Implementation { get; init; } = null!;
}