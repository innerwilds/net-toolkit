using System;
using System.Collections;
using System.Collections.Generic;

namespace FrameworkAgnostic.Pagination;

public class CollectionView<T> : IEnumerable<T>
{
    private readonly T[] _collection;
    private readonly Func<T[], int, int> _getChunk;

    public CollectionView(Func<T[], int, int> getChunk, int viewCount)
    {
        _getChunk = getChunk;
        _collection = new T[viewCount];
    }

    /// <summary>
    ///     The length of items in the represented collection
    /// </summary>
    public int Length { get; }

    /// <summary>
    ///     Enumerates only current frame view items.
    /// </summary>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public IEnumerator<T> GetEnumerator()
    {
        return ((IEnumerable<T>)_collection).GetEnumerator();
    }

    /// <summary>
    ///     Enumerates only current frame view items
    /// </summary>
    /// <returns></returns>
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}