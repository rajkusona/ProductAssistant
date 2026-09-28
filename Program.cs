using System.ComponentModel;
using System.Diagnostics;
using System.Net.Sockets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.SemanticKernel;
using StackExchange.Redis;
using SkfProductAssistant.Functions.Services;
using SkfProductAssistant.Functions.Agents;
using SkfProductAssistant.Functions.Configuration;

Log4NetConfigurator.Configure();
await EnsureAzuriteAsync();

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureAppConfiguration((context, configuration) =>
    {
        configuration.SetBasePath(AppContext.BaseDirectory);
        configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);
        configuration.AddUserSecrets<Program>(optional: true);
        configuration.AddEnvironmentVariables();
    })
    .ConfigureServices((context, services) =>
    {
        var settings = AzureOpenAiSettings.FromConfiguration(context.Configuration);
        var redisSettings = RedisSettings.FromConfiguration(context.Configuration);
        services.AddSingleton(settings);
        services.AddSingleton(redisSettings);
        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisSettings.ConnectionString));
        services.AddSingleton<IConversationStore, RedisConversationStore>();
        services.AddSingleton<ProductCatalog>();
        services.AddSingleton<Kernel>(sp =>
        {
            var builder = Kernel.CreateBuilder();
            builder.AddAzureOpenAIChatCompletion(
                settings.Deployment,
                settings.Endpoint,
                settings.ApiKey);
            return builder.Build();
        });
        services.AddSingleton<QuestionAgent>();
        services.AddSingleton<FeedbackAgent>();
        services.AddSingleton<Orchestrator>();
    })
    .Build();

await host.RunAsync();

static async Task EnsureAzuriteAsync()
{
    if (!string.Equals(Environment.GetEnvironmentVariable("AzureWebJobsStorage"), "UseDevelopmentStorage=true", StringComparison.OrdinalIgnoreCase))
    {
        return;
    }

    var ports = new[] { 10000, 10001, 10002 };
    var portStates = await Task.WhenAll(ports.Select(IsPortOpenAsync));
    if (portStates.All(open => open))
    {
        return;
    }

    var storageLocation = Path.Combine(Path.GetTempPath(), "skf-product-assistant-azurite");
    Directory.CreateDirectory(storageLocation);
    var startInfo = new ProcessStartInfo
    {
        FileName = "azurite.cmd",
        Arguments = $"--location \"{storageLocation}\" --blobPort 10000 --queuePort 10001 --tablePort 10002",
        WorkingDirectory = AppContext.BaseDirectory,
        UseShellExecute = false,
        CreateNoWindow = true
    };

    try
    {
        Process.Start(startInfo);
    }
    catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
    {
        Console.Error.WriteLine($"Unable to start Azurite automatically: {exception.Message}");
        return;
    }

    var deadline = DateTime.UtcNow.AddSeconds(15);
    while (DateTime.UtcNow < deadline)
    {
        portStates = await Task.WhenAll(ports.Select(IsPortOpenAsync));
        if (portStates.All(open => open))
        {
            return;
        }

        await Task.Delay(250);
    }

    Console.Error.WriteLine("Azurite did not become ready within 15 seconds.");
}

static async Task<bool> IsPortOpenAsync(int port)
{
    using var client = new TcpClient();
    try
    {
        await client.ConnectAsync("127.0.0.1", port).WaitAsync(TimeSpan.FromMilliseconds(250));
        return true;
    }
    catch (Exception exception) when (exception is SocketException or TimeoutException)
    {
        return false;
    }
}