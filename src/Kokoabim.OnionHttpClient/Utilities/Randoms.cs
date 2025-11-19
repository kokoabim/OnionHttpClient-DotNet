namespace Kokoabim.OnionHttpClient;

public static class Randoms
{
    private static readonly char[] _chars = "abcdefghijklmnopqrstuvwxyz0123456789".ToCharArray();
    private static readonly Random _random = new(DateTime.Now.Millisecond);
    private static readonly HashSet<int> _usedInts = [];
    private static readonly HashSet<string> _usedStrings = [];

    /// <summary>
    /// Generates a random string (using characters from a-z and 0-9) of the specified length.
    /// </summary>
    public static string String(int length)
    {
        var buffer = new char[length];
        for (int i = 0; i < length; i++) buffer[i] = _chars[_random.Next(_chars.Length)];
        return new string(buffer);
    }

    /// <summary>
    /// Generates a unique, random integer within the specified range.
    /// </summary>
    public static int UniqueInt(int minValue, int maxValue)
    {
        lock (_usedInts)
        {
            do
            {
                var value = _random.Next(minValue, maxValue);
                if (!_usedInts.Contains(value))
                {
                    _ = _usedInts.Add(value);
                    return value;
                }
            } while (true);
        }
    }

    /// <summary>
    /// Generates a unique, random string (using characters from a-z and 0-9) of the specified length.
    /// </summary>
    public static string UniqueString(int length)
    {
        lock (_usedStrings)
        {
            do
            {
                var value = String(length);
                if (!_usedStrings.Contains(value))
                {
                    _ = _usedStrings.Add(value);
                    return value;
                }
            } while (true);
        }
    }
}