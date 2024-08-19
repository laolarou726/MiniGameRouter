using System.Runtime.CompilerServices;

namespace MiniGameRouter.Models.Trie;

internal static class SpanException
{
    public static void ThrowIfNullOrEmpty(ReadOnlySpan<char> argument, [CallerArgumentExpression(nameof(argument))] string? paramName = null)
    {
        if (argument.IsEmpty)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(argument.ToString(), paramName);
        }
    }
}