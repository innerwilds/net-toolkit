using System;

namespace FrameworkAgnostic.Navigation;

public interface IKeepAlive : IDisposable
{
    public bool KeepAlive { get; }
}