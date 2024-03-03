using System;

namespace FrameworkAgnostic.Navigation;

public interface IHistory<TPage>
{
    public int Length { get; }
    public void Push(Type targetPageType);
    public void Push<TSpecificPage>() where TSpecificPage : TPage;

    public bool CanGo(int delta);
    public void Go(int delta);

    public void Clear();
}