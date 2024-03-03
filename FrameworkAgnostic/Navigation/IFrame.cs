using FrameworkAgnostic.Messaging;

namespace FrameworkAgnostic.Navigation;

public interface IFrame<TPage> : IRecipient<HistoryMessage>
{
    /// <summary>
    ///     The unique name of the frame in some logical context
    /// </summary>
    public string Name { get; }

    /// <summary>
    ///     The navigation manager for the Frame
    /// </summary>
    public IHistory<TPage> History { get; }
}