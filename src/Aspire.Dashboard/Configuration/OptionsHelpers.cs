using System.Diagnostics.CodeAnalysis;

namespace Aspire.Dashboard.Configuration;

public static class OptionsHelpers
{
    public static bool TryParseBindingAddress(string address, [NotNullWhen(true)] out BindingAddress? bindingAddress)
    {
        try
        {
            bindingAddress = BindingAddress.Parse(address);
            return true;
        }
        catch
        {
            bindingAddress = null;
            return false;
        }
    }
}
