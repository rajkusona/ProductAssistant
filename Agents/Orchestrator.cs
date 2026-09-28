using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using log4net;
using SkfProductAssistant.Functions.Models;
using SkfProductAssistant.Functions.Services;

namespace SkfProductAssistant.Functions.Agents;

public sealed class Orchestrator
{
    private static readonly ILog Logger = LogManager.GetLogger(typeof(Orchestrator));
    private readonly Kernel kernel;
    private readonly QuestionAgent questionAgent;
    private readonly FeedbackAgent feedbackAgent;
    private readonly IConversationStore store;

    public Orchestrator(Kernel kernel, QuestionAgent questionAgent, FeedbackAgent feedbackAgent, IConversationStore store)
    {
        this.kernel = kernel;
        this.questionAgent = questionAgent;
        this.feedbackAgent = feedbackAgent;
        this.store = store;
    }

    public async Task<(string Reply, string Route)> HandleAsync(string conversationId, string message, CancellationToken cancellationToken)
    {
        Logger.Info("Starting orchestration.");
        var state = store.Get(conversationId);
        var route = await ClassifyAsync(message, cancellationToken);
        Logger.Info($"Intent classified. Route={route}");
        if (route == "feedback")
        {
            return (await feedbackAgent.CaptureAsync(conversationId, message, state, cancellationToken), route);
        }

        var answer = await questionAgent.AnswerAsync(conversationId, message, state, cancellationToken);
        if (answer.Lookup is not null && answer.Lookup.Found)
        {
            store.Set(conversationId, state with
            {
                LastDesignation = answer.Lookup.Designation,
                LastAttribute = answer.Lookup.Attribute,
                LastAnswer = answer.Reply
            });
        }

        return (answer.Reply, "question");
    }

    private async Task<string> ClassifyAsync(string message, CancellationToken cancellationToken)
    {
        var prompt = $"Classify the user message as exactly one lowercase word: feedback or question. Feedback includes corrections, helpful/unhelpful comments, and notes about a prior answer. Message: {message}";
        var settings = new OpenAIPromptExecutionSettings();
        var functionResult = await kernel.InvokePromptAsync(prompt, new KernelArguments(settings), cancellationToken: cancellationToken);
        var result = (functionResult.GetValue<string>() ?? functionResult.ToString()).Trim().ToLowerInvariant();
        return result.Contains("feedback", StringComparison.Ordinal) ? "feedback" : "question";
    }
}