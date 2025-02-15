namespace Aspire.Dashboard.Configuration;

internal static class IConfigurationExtensions
{
    /// <summary>
    /// Gets the named configuration value as a boolean.
    /// </summary>
    /// <remarks>
    /// Parses <c>true</c> and <c>false</c>, along with integer values (where non-zero is <see langword="true"/>).
    /// </remarks>
    /// <param name="configuration">The <see cref="IConfiguration"/> this method extends.</param>
    /// <param name="key">The configuration key.</param>
    /// <returns>The parsed value, or <see langword="null"/> if no value exists or it couldn't be parsed.</returns>
    public static bool? GetBool(this IConfiguration configuration, string key)
    {
        var value = configuration[key];

        if (value is null or [])
        {
            return null;
        }
        else if (bool.TryParse(value, out var b))
        {
            return b;
        }
        else if (int.TryParse(value, out var i))
        {
            return i != 0;
        }

        return null;
    }

    /// <summary>
    /// Gets the named configuration value as a boolean.
    /// </summary>
    /// <remarks>
    /// Parses <c>true</c> and <c>false</c>, along with <c>1</c> and <c>0</c>.
    /// </remarks>
    /// <param name="configuration">The <see cref="IConfiguration"/> this method extends.</param>
    /// <param name="key">The configuration key.</param>
    /// <param name="defaultValue">A default value, for when the configuration value is unspecified or white space.</param>
    /// <returns></returns>
    public static bool GetBool(this IConfiguration configuration, string key, bool defaultValue)
    {
        return configuration.GetBool(key) ?? defaultValue;
    }
}

