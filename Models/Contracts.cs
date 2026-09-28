namespace SkfProductAssistant.Functions.Models;

public sealed record ChatRequest(Guid? ConversationId, string? Message);

public sealed record ChatResponse(string ConversationId, string CorrelationId, string Reply, string Route);

public sealed record ConversationState(
    string? LastDesignation = null,
    string? LastAttribute = null,
    string? LastAnswer = null);

public sealed record LookupResult(
    bool Found,
    string Designation,
    string Attribute,
    string? Value,
    string? Unit,
    string? Source);

public sealed record FeedbackRecord(
    string ConversationId,
    string? Designation,
    string? Attribute,
    string Message,
    DateTimeOffset CreatedAtUtc);