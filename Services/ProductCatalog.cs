using System.Text.Json;
using SkfProductAssistant.Functions.Models;
using StackExchange.Redis;

namespace SkfProductAssistant.Functions.Services;

public sealed class ProductCatalog
{
    private readonly IReadOnlyDictionary<string, JsonElement> products;
    private readonly IDatabase cache;

    public ProductCatalog(IConnectionMultiplexer connection)
    {
        cache = connection.GetDatabase();
        var root = AppContext.BaseDirectory;
        var files = Directory.EnumerateFiles(Path.Combine(root, "products"), "*.json");
        var loaded = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            using var document = JsonDocument.Parse(File.ReadAllText(file));
            var product = document.RootElement.Clone();
            var designation = product.GetProperty("designation").GetString();
            if (!string.IsNullOrWhiteSpace(designation))
            {
                loaded[Normalize(designation)] = product;
            }
        }

        products = loaded;
    }

    public LookupResult Lookup(string designation, string attribute)
    {
        var normalizedDesignation = Normalize(designation);
        var normalizedAttribute = Normalize(attribute);
        var cacheKey = $"skf:catalog:lookup:{Hash(normalizedDesignation)}:{Hash(normalizedAttribute)}";
        var cached = cache.StringGet(cacheKey);
        if (cached.HasValue)
        {
            return JsonSerializer.Deserialize<LookupResult>(cached.ToString())!;
        }

        LookupResult result;
        if (!products.TryGetValue(normalizedDesignation, out var product))
        {
            result = new LookupResult(false, designation.Trim(), attribute.Trim(), null, null, null);
            Cache(cacheKey, result);
            return result;
        }

        foreach (var group in new[] { "dimensions", "properties", "performance", "logistics", "specifications" })
        {
            if (!product.TryGetProperty(group, out var values) || values.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var item in values.EnumerateArray())
            {
                var name = item.GetProperty("name").GetString() ?? string.Empty;
                if (Normalize(name) != normalizedAttribute && !Aliases(normalizedAttribute).Contains(Normalize(name)))
                {
                    continue;
                }

                var value = item.GetProperty("value");
                var formattedValue = value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
                var unit = item.TryGetProperty("unit", out var unitElement) ? unitElement.GetString() : null;
                result = new LookupResult(true, product.GetProperty("designation").GetString()!, name, formattedValue, unit, group);
                Cache(cacheKey, result);
                return result;
            }
        }

        result = new LookupResult(false, product.GetProperty("designation").GetString()!, attribute.Trim(), null, null, null);
        Cache(cacheKey, result);
        return result;
    }

    private void Cache(string key, LookupResult result) => cache.StringSet(key, JsonSerializer.Serialize(result), TimeSpan.FromMinutes(30));

    private static string Hash(string value) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static IEnumerable<string> Aliases(string attribute) => attribute switch
    {
        "diameter" or "outside diameter" => new[] { "outside diameter" },
        "bore" or "bore diameter" or "inner diameter" => new[] { "bore diameter" },
        "height" => new[] { "pack height" },
        "length" => new[] { "pack length" },
        "width" => new[] { "width", "pack width" },
        _ => Array.Empty<string>()
    };

    private static string Normalize(string value) => string.Join(' ', value.Trim().ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}