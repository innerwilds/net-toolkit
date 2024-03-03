using System.Collections;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FrameworkAgnostic.Messaging.Messages;

/// <summary>
///     A <see langword="class" /> for request messages that can receive multiple replies, which can either be used
///     directly or through derived classes.
/// </summary>
/// <typeparam name="T">The type of request to make.</typeparam>
public class CollectionRequestMessage<T> : IEnumerable<T>
{
    private readonly List<T> responses = new();

    /// <summary>
    ///     Gets the message responses.
    /// </summary>
    public IReadOnlyCollection<T> Responses => responses;

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public IEnumerator<T> GetEnumerator()
    {
        return responses.GetEnumerator();
    }

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    /// <summary>
    ///     Replies to the current request message.
    /// </summary>
    /// <param name="response">The response to use to reply to the request message.</param>
    public void Reply(T response)
    {
        responses.Add(response);
    }
}