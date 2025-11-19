namespace Kokoabim.OnionHttpClient;

public static class HttpClientExtensions
{
    public static bool SetHeader(this HttpClient source, string name, string value, bool overwrite = true)
    {
        if (overwrite && source.DefaultRequestHeaders.Contains(name)) _ = source.DefaultRequestHeaders.Remove(name);
        return source.DefaultRequestHeaders.TryAddWithoutValidation(name, value);
    }

    public static bool SetHeader(this HttpClient source, string name, IEnumerable<string> values, bool overwrite = true)
    {
        if (overwrite && source.DefaultRequestHeaders.Contains(name)) _ = source.DefaultRequestHeaders.Remove(name);
        return source.DefaultRequestHeaders.TryAddWithoutValidation(name, values);
    }
}