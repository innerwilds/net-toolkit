using FrameworkAgnostic.DependencyInjection;

namespace FrameworkAgnostic.Navigation;

public class History<TPage> : IHistory<TPage> where TPage : class
{
    private readonly ICollection<HistoryEntry> _history = new HashSet<HistoryEntry>();
    private readonly object _historyLockObj = new();

    private readonly Action<TPage> _onNavigated;
    private int _currentHistoryIndex = -1;
    private HistoryEntry? _entry;

    public History(Action<TPage> onNavigated)
    {
        _onNavigated = onNavigated;
    }

    public IServiceProvider ServiceProvider { get; set; } = Ioc.Default;

    public void Push(Type targetPageType)
    {
        lock (_historyLockObj)
        {
            ExitCurrentEntry();

            var entry = new HistoryEntry
            {
                PageType = typeof(TPage)
            };

            _history.Add(entry);
            _currentHistoryIndex = _history.Count - 1;
            _entry = entry;

            entry.Instantiate(ServiceProvider);

            _onNavigated(entry.PageInstance!);
        }
    }

    public void Push<TSpecificPage>() where TSpecificPage : TPage
    {
        Push(typeof(TSpecificPage));
    }

    public bool CanGo(int delta)
    {
        if (_history.Count == 0 || delta == 0) return false;

        var entryIndex = _currentHistoryIndex + delta;

        return entryIndex >= 0 && entryIndex < _history.Count;
    }

    public void Go(int delta)
    {
        lock (_historyLockObj)
        {
            if (_history.Count == 0 || delta == 0)
                return;

            var entryIndex = _currentHistoryIndex + delta;

            if (entryIndex < 0 || entryIndex >= _history.Count)
                return;

            var entry = _history.ElementAt(entryIndex);

            _currentHistoryIndex = entryIndex;
            _entry = entry;

            _entry.Instantiate(ServiceProvider);

            _onNavigated(_entry.PageInstance!);
        }
    }

    public void Clear()
    {
        lock (_historyLockObj)
        {
            _history.Clear();
            _currentHistoryIndex = -1;
        }
    }

    public int Length => _history.Count;

    private void ExitCurrentEntry()
    {
        var entry = _entry;

        if (entry?.PageInstance is IKeepAlive { KeepAlive: false } page)
        {
            page.Dispose();
            entry.PageInstance = null;
        }
    }

    private class HistoryEntry
    {
        internal Type PageType { get; init; }
        internal TPage? PageInstance { get; set; }

        internal void Instantiate(IServiceProvider serviceProvider)
        {
            if (PageInstance != null) return;

            PageInstance = (TPage?)serviceProvider.GetService(PageType) ??
                           throw new ArgumentNullException($"Can't resolve page {PageType}");
        }
    }
}