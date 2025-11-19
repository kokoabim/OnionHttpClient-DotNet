using System.Diagnostics;

namespace Kokoabim.OnionHttpClient;

public static class ExceptionExtensions
{
    /// <summary>
    /// Gets all messages of an exception and its inner exceptions.
    /// </summary>
    [StackTraceHidden]
    public static string GetMessages(this Exception source, bool includeTypes = false, bool multiLine = false)
    {
        if (source is AggregateException aggregateException)
        {
            if (aggregateException.InnerExceptions.Count == 1)
            {
                return aggregateException.InnerExceptions[0].GetMessages(includeTypes, multiLine);
            }
            else if (aggregateException.InnerExceptions.Count > 1)
            {
                return string.Join(
                    multiLine ? Environment.NewLine : "; ",
                    aggregateException.InnerExceptions.Select((ex, i) => (includeTypes ? $"[{i}] " : "") + ex.GetMessages(includeTypes, multiLine)));
            }
            else if (aggregateException.InnerException != null)
            {
                return aggregateException.InnerException.GetMessages(includeTypes, multiLine);
            }
        }

        var message = (includeTypes ? $"{source.GetType().Name}: " : "") + source.Message;

        if (source.InnerException != null) message +=
            (multiLine ? Environment.NewLine : " —> ") + source.InnerException.GetMessages(includeTypes, multiLine);

        return message;
    }
}