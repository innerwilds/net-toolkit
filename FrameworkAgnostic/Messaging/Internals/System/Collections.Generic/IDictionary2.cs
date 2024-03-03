namespace FrameworkAgnostic.Messaging.Internals.System.Collections.Generic;

/// <summary>
///     A base interface masking <see cref="Dictionary2{TKey,TValue}" /> instances and exposing non-generic
///     functionalities.
/// </summary>
internal interface IDictionary2
{
    /// <summary>
    ///     Gets the count of entries in the dictionary.
    /// </summary>
    int Count { get; }

    /// <summary>
    ///     Clears the current dictionary.
    /// </summary>
    void Clear();
}