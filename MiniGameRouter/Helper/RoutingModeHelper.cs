using System.Diagnostics.CodeAnalysis;

namespace MiniGameRouter.Helper;

public static class RoutingModeHelper
{
    public static bool ResolveRoute(this string raw, [NotNullWhen(true)] out (string ModeStr, string? HashKey)? result)
    {
        if (raw.StartsWith("random", StringComparison.OrdinalIgnoreCase))
        {
            result = ("Random", null);
            return true;
        }
        
        if (raw.StartsWith("weighted", StringComparison.OrdinalIgnoreCase))
        {
            result = ("Weighted", null);
            return true;
        }
        
        if (raw.StartsWith("hash", StringComparison.OrdinalIgnoreCase))
        {
            var parts = raw.Split(";", StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2)
            {
                result = null;
                return false;
            }
            
            result = ("Hash", parts[1]);
            return true;
        }

        result = null;
        return false;
    }
}