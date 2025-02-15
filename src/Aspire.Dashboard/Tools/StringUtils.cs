using System.Diagnostics.CodeAnalysis;

namespace Aspire.Dashboard.Tools;

internal static class StringUtils
{
    public static bool TryGetUriFromDelimitedString([NotNullWhen(true)] string? input, string delimiter, [NotNullWhen(true)] out Uri? uri)
    {
        if (!string.IsNullOrEmpty(input)
            && input.Split(delimiter) is { Length: > 0 } splitInput
            && Uri.TryCreate(splitInput[0], UriKind.Absolute, out uri))
        {
            return true;
        }
        else
        {
            uri = null;
            return false;
        }
    }
}
