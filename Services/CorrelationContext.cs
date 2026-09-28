using log4net;

namespace SkfProductAssistant.Functions.Services;

public static class CorrelationContext
{
    public static IDisposable Begin(string correlationId)
    {
        LogicalThreadContext.Properties["CorrelationId"] = correlationId;
        return new Scope();
    }

    private sealed class Scope : IDisposable
    {
        public void Dispose() => LogicalThreadContext.Properties.Remove("CorrelationId");
    }
}