using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SkfProductAssistant.Functions.Models;
using StackExchange.Redis;

namespace SkfProductAssistant.Functions.Services;

public interface IConversationStore
{
    ConversationState Get(string conversationId);
    void Set(string conversationId, ConversationState state);
    void AddFeedback(FeedbackRecord feedback);
}

public sealed class InMemoryConversationStore : IConversationStore
{
    private readonly ConcurrentDictionary<string, ConversationState> states = new(StringComparer.Ordinal);
    private readonly ConcurrentQueue<FeedbackRecord> feedback = new();

    public ConversationState Get(string conversationId) => states.TryGetValue(conversationId, out var state) ? state : new();
    public void Set(string conversationId, ConversationState state) => states[conversationId] = state;
    public void AddFeedback(FeedbackRecord record) => feedback.Enqueue(record);
}

public sealed class RedisConversationStore : IConversationStore
{
    private static readonly TimeSpan StateRetention = TimeSpan.FromHours(24);
    private static readonly TimeSpan FeedbackRetention = TimeSpan.FromDays(90);
    private const string StatePrefix = "skf:conversation:state:";
    private const string FeedbackPrefix = "skf:conversation:feedback:";
    private readonly IDatabase database;

    public RedisConversationStore(IConnectionMultiplexer connection)
    {
        database = connection.GetDatabase();
    }

    public ConversationState Get(string conversationId)
    {
        var value = database.StringGet(StateKey(conversationId));
        return value.HasValue
            ? JsonSerializer.Deserialize<ConversationState>(value.ToString()) ?? new()
            : new();
    }

    public void Set(string conversationId, ConversationState state)
    {
        database.StringSet(StateKey(conversationId), JsonSerializer.Serialize(state), StateRetention);
    }

    public void AddFeedback(FeedbackRecord feedback)
    {
        database.ListLeftPush(FeedbackKey(feedback.ConversationId), JsonSerializer.Serialize(feedback));
        database.KeyExpire(FeedbackKey(feedback.ConversationId), FeedbackRetention);
    }

    private static RedisKey StateKey(string conversationId) => StatePrefix + Hash(conversationId);
    private static RedisKey FeedbackKey(string conversationId) => FeedbackPrefix + Hash(conversationId);

    private static string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}