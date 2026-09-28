using System.ComponentModel;
using System.Text.Json;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using SkfProductAssistant.Functions.Models;
using SkfProductAssistant.Functions.Services;

namespace SkfProductAssistant.Functions.Agents;

public sealed class FeedbackAgent
{
    private readonly Kernel kernel;
    private readonly IConversationStore store;

    public FeedbackAgent(Kernel kernel, IConversationStore store)
    {
        this.kernel = kernel;
        this.store = store;
        this.kernel.Plugins.Add(KernelPluginFactory.CreateFromObject(new FeedbackFunctions(store), "Feedback"));
    }

    public async Task<string> CaptureAsync(string conversationId, string message, ConversationState state, CancellationToken cancellationToken)
    {
        var prompt = $"""
            You capture user feedback about product answers.
            Call Feedback.save_feedback exactly once. Use the user's explicitly referenced designation and attribute;
            otherwise use the prior state. Pass the conversation ID and preserve the user's correction in the message.
            Conversation ID: {conversationId}
            Full feedback message: {message}
            After the tool call, confirm briefly that feedback was saved.
            Prior designation: {state.LastDesignation ?? "none"}
            Prior attribute: {state.LastAttribute ?? "none"}
            User message: {message}
            """;
        var settings = new OpenAIPromptExecutionSettings { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() };
        var result = await kernel.InvokePromptAsync(prompt, new KernelArguments(settings), cancellationToken: cancellationToken);
        return result.ToString().Trim();
    }

    private sealed class FeedbackFunctions
    {
        private readonly IConversationStore store;

        public FeedbackFunctions(IConversationStore store) => this.store = store;

        [KernelFunction("save_feedback")]
        [Description("Persist feedback about the latest product answer.")]
        public string SaveFeedback(
            [Description("Conversation ID from the current request.")] string conversationId,
            [Description("The complete user feedback message.")] string feedbackMessage,
            [Description("Product designation, or the prior designation when omitted by the user.")] string? designation,
            [Description("Product attribute, or the prior attribute when omitted by the user.")] string? attribute)
        {
            var record = new FeedbackRecord(conversationId, designation, attribute, feedbackMessage, DateTimeOffset.UtcNow);
            store.AddFeedback(record);
            return JsonSerializer.Serialize(new { saved = true, designation = record.Designation, attribute = record.Attribute });
        }
    }
}