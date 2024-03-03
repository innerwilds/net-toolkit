using System.ComponentModel;

namespace FrameworkAgnostic.Collections.Internals;

/// <summary>
///     A helper type for the <see cref="ObservableGroup{TKey, TValue}" /> type.
/// </summary>
internal static class ObservableGroupHelper
{
    /// <summary>
    ///     The cached <see cref="PropertyChangedEventArgs" /> for <see cref="IReadOnlyObservableGroup.Key" />
    /// </summary>
    public static readonly PropertyChangedEventArgs KeyChangedEventArgs = new(nameof(IReadOnlyObservableGroup.Key));
}