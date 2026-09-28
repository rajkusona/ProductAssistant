using Microsoft.Extensions.Configuration;

namespace SkfProductAssistant.Functions.Configuration;

public sealed record AzureOpenAiSettings(
    string Endpoint,
    string ApiKey,
    string Deployment,
    string ApiVersion)
{
    public static AzureOpenAiSettings FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection("AzureOpenAI");
        var endpoint = section["Endpoint"] ?? throw new InvalidOperationException("AzureOpenAI:Endpoint is required.");
        var apiKey = section["ApiKey"] ?? throw new InvalidOperationException("AzureOpenAI:ApiKey is required.");
        var deployment = section["Deployment"] ?? throw new InvalidOperationException("AzureOpenAI:Deployment is required.");
        var apiVersion = section["ApiVersion"] ?? "2024-10-21";

        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var parsed) || parsed.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException("AzureOpenAI:Endpoint must be an HTTPS URL.");
        }

        return new AzureOpenAiSettings(endpoint, apiKey, deployment, apiVersion);
    }
}

public sealed record RedisSettings(string ConnectionString)
{
    public static RedisSettings FromConfiguration(IConfiguration configuration)
    {
        var connectionString = configuration["Redis:ConnectionString"];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Redis:ConnectionString is required.");
        }

        return new RedisSettings(connectionString);
    }
}