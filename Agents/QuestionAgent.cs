using System.ComponentModel;
using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using SkfProductAssistant.Functions.Models;
using SkfProductAssistant.Functions.Services;

namespace SkfProductAssistant.Functions.Agents;

public sealed class QuestionAgent
{
    private readonly Kernel kernel;
    private readonly CatalogFunctions functions;

    public QuestionAgent(Kernel kernel, ProductCatalog catalog)
    {
        this.kernel = kernel;
        functions = new CatalogFunctions(catalog);
        this.kernel.Plugins.Add(KernelPluginFactory.CreateFromObject(functions, "Catalog"));
    }

    public async Task<(string Reply, LookupResult? Lookup)> AnswerAsync(string conversationId, string message, ConversationState state, CancellationToken cancellationToken)
    {
        var prompt = $"""
            You answer product attribute questions using only the Catalog plugin.
            Call Catalog.lookup_product_attribute exactly once for a question. Never invent or infer a value.
            If the function reports Found=false, say the information cannot be found for the designation and attribute.
            Return one concise sentence. Resolve pronouns using the prior state when needed.
            Prior designation: {state.LastDesignation ?? "none"}
            Prior attribute: {state.LastAttribute ?? "none"}
            Conversation ID: {conversationId}
            User message: {message}
            """;

        var settings = new OpenAIPromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
        };
        var result = await kernel.InvokePromptAsync(prompt, new KernelArguments(settings), cancellationToken: cancellationToken);
        var lookup = functions.GetLookup(conversationId);
        if (lookup is null)
        {
            return ("Sorry, I couldn't verify that information in the product datasheets.", null);
        }

        if (!lookup.Found)
        {
            return ($"Sorry, I can't find that information for '{lookup.Designation}'. Please try another designation or attribute.", lookup);
        }

        var formattedValue = string.IsNullOrWhiteSpace(lookup.Unit) ? lookup.Value : $"{lookup.Value} {lookup.Unit}";
        return ($"The {lookup.Attribute.ToLowerInvariant()} of the {lookup.Designation} bearing is {formattedValue}.", lookup);
    }

    private sealed class CatalogFunctions
    {
        private readonly ProductCatalog catalog;
        private readonly ConcurrentDictionary<string, LookupResult> lookups = new(StringComparer.Ordinal);

        public CatalogFunctions(ProductCatalog catalog) => this.catalog = catalog;

        [KernelFunction("lookup_product_attribute")]
        [Description("Look up one exact product attribute in the local authoritative datasheets.")]
        public string LookupProductAttribute(
            [Description("Conversation ID from the current request.")] string conversationId,
            [Description("The exact product designation, such as 6205 or 6205 N.")] string designation,
            [Description("The requested attribute, such as width, diameter, or bore diameter.")] string attribute)
        {
            var lookup = catalog.Lookup(designation, attribute);
            lookups[conversationId] = lookup;
            return JsonSerializer.Serialize(lookup);
        }

        public LookupResult? GetLookup(string conversationId) => lookups.TryRemove(conversationId, out var lookup) ? lookup : null;
    }
}