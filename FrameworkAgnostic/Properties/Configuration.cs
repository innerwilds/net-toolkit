using System.ComponentModel;

namespace FrameworkAgnostic.Properties;

/// <summary>
///     A container for all shared <see cref="AppContext" /> configuration switches for the MVVM Toolkit.
/// </summary>
internal static class Configuration
{
    /// <summary>
    ///     The configuration property name for <see cref="IsINotifyPropertyChangingDisabled" />.
    /// </summary>
    private const string DisableINotifyPropertyChangingSupport = "MVVMTOOLKIT_DISABLE_INOTIFYPROPERTYCHANGING";

    /// <summary>
    ///     Indicates whether or not support for <see cref="INotifyPropertyChanging" /> is disabled.
    /// </summary>
    public static readonly bool IsINotifyPropertyChangingDisabled =
        GetConfigurationValue(DisableINotifyPropertyChangingSupport);

    /// <summary>
    ///     Gets a configuration value for a specified property.
    /// </summary>
    /// <param name="propertyName">The property name to retrieve the value for.</param>
    /// <returns>The value of the specified configuration setting.</returns>
    private static bool GetConfigurationValue(string propertyName)
    {
        if (AppContext.TryGetSwitch(propertyName, out var isEnabled)) return isEnabled;

        return false;
    }
}