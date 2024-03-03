namespace FrameworkAgnostic.Navigation;

public abstract class FrameBase<TPage> : IFrame<TPage>
{
    protected FrameBase(string name)
    {
        Name = name;
    }

    public string Name { get; }
    public abstract IHistory<TPage> History { get; }

    public void Receive(HistoryMessage message)
    {
        if (!message.FrameName.Equals(message.FrameName))
            return;

        History.Push(message.PageType);
    }
}