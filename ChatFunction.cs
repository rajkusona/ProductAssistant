using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using log4net;
using SkfProductAssistant.Functions.Agents;
using SkfProductAssistant.Functions.Models;
using SkfProductAssistant.Functions.Services;

namespace SkfProductAssistant.Functions;

public sealed class ChatFunction
{
    private static readonly ILog Logger = LogManager.GetLogger(typeof(ChatFunction));
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly Orchestrator orchestrator;

    public ChatFunction(Orchestrator orchestrator) => this.orchestrator = orchestrator;

    [Function("chat")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "chat")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var correlationId = GetCorrelationId(request);
        using var correlationScope = CorrelationContext.Begin(correlationId);
        Logger.Info("Received chat request.");

        ChatRequest? input;
        try
        {
            input = await JsonSerializer.DeserializeAsync<ChatRequest>(request.Body, JsonOptions, cancellationToken);
        }
        catch (JsonException)
        {
            Logger.Warn("Rejected request with malformed JSON.");
            return await Error(request, HttpStatusCode.BadRequest, correlationId, "Request body must be valid JSON.");
        }

        if (input is null || string.IsNullOrWhiteSpace(input.Message) || input.Message.Length > 2000)
        {
            Logger.Warn("Rejected request with missing or oversized message.");
            return await Error(request, HttpStatusCode.BadRequest, correlationId, "Provide a message between 1 and 2000 characters.");
        }

        var conversationId = (input.ConversationId ?? Guid.NewGuid()).ToString();
        try
        {
            var result = await orchestrator.HandleAsync(conversationId, input.Message.Trim(), cancellationToken);
            Logger.Info($"Completed chat request. Route={result.Route}");
            var response = request.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new ChatResponse(conversationId, correlationId, result.Reply, result.Route), cancellationToken);
            return response;
        }
        catch (Exception exception)
        {
            Logger.Error("Chat request failed.", exception);
            var response = request.CreateResponse(HttpStatusCode.InternalServerError);
            await response.WriteAsJsonAsync(new { correlationId, error = "The request could not be completed." });
            return response;
        }
    }

    private static async Task<HttpResponseData> Error(HttpRequestData request, HttpStatusCode status, string correlationId, string message)
    {
        var response = request.CreateResponse(status);
        await response.WriteAsJsonAsync(new { correlationId, error = message });
        return response;
    }

    private static string GetCorrelationId(HttpRequestData request)
    {
        if (request.Headers.TryGetValues("x-correlation-id", out var values))
        {
            var supplied = values.FirstOrDefault()?.Trim();
            if (Guid.TryParse(supplied, out var suppliedGuid))
            {
                return suppliedGuid.ToString();
            }
        }

        return Guid.NewGuid().ToString();
    }
}