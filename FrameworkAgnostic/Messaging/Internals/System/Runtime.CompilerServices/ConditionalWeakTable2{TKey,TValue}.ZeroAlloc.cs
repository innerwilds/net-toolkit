#if NET6_0_OR_GREATER

using System;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Threading;

namespace FrameworkAgnostic.Messaging.Internals.System.Runtime.CompilerServices;

/// <summary>
///     A custom <see cref="ConditionalWeakTable{TKey,TValue}" /> instance that is specifically optimized to be used
///     by <see cref="WeakReferenceMessenger" />. In particular, it offers zero-allocation enumeration of stored items.
/// </summary>
/// <typeparam name="TKey">Tke key of items to store in the table.</typeparam>
/// <typeparam name="TValue">The values to store in the table.</typeparam>
internal sealed class ConditionalWeakTable2<TKey, TValue>
    where TKey : class
    where TValue : class?
{
    /// <summary>
    ///     Initial length of the table. Must be a power of two.
    /// </summary>
    private const int InitialCapacity = 8;

    /// <summary>
    ///     This lock protects all mutation of data in the table. Readers do not take this lock.
    /// </summary>
    private readonly object lockObject;

    /// <summary>
    ///     The actual storage for the table; swapped out as the table grows.
    /// </summary>
    private volatile Container container;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ConditionalWeakTable2{TKey, TValue}" /> class.
    /// </summary>
    public ConditionalWeakTable2()
    {
        lockObject = new object();
        container = new Container(this);
    }

    /// <inheritdoc cref="ConditionalWeakTable{TKey, TValue}.TryGetValue(TKey, out TValue)" />
    public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
        return container.TryGetValueWorker(key, out value);
    }

    /// <summary>
    ///     Tries to add a new pair to the table.
    /// </summary>
    /// <param name="key">The key to add.</param>
    /// <param name="value">The value to associate with key.</param>
    public bool TryAdd(TKey key, TValue value)
    {
        lock (lockObject)
        {
            var entryIndex = container.FindEntry(key, out _);

            if (entryIndex != -1) return false;

            CreateEntry(key, value);

            return true;
        }
    }

    /// <inheritdoc cref="ConditionalWeakTable{TKey, TValue}.Remove(TKey)" />
    public bool Remove(TKey key)
    {
        lock (lockObject)
        {
            return container.Remove(key);
        }
    }

    /// <inheritdoc
    ///     cref="ConditionalWeakTable{TKey, TValue}.GetValue(TKey, ConditionalWeakTable{TKey, TValue}.CreateValueCallback)" />
    [UnconditionalSuppressMessage(
        "ReflectionAnalysis",
        "IL2091",
        Justification =
            "ConditionalWeakTable<TKey, TValue> is only referenced to reuse the callback delegate type, but no value is ever created through reflection.")]
    public TValue GetValue(TKey key, ConditionalWeakTable<TKey, TValue>.CreateValueCallback createValueCallback)
    {
        return TryGetValue(key, out var existingValue) ? existingValue : GetValueLocked(key, createValueCallback);
    }

    /// <summary>
    ///     Implements the functionality for
    ///     <see cref="GetValue(TKey, ConditionalWeakTable{TKey, TValue}.CreateValueCallback)" /> under a lock.
    /// </summary>
    /// <param name="key">The input key.</param>
    /// <param name="createValueCallback">The callback to use to create a new item.</param>
    /// <returns>The new <typeparamref name="TValue" /> item to store.</returns>
    [UnconditionalSuppressMessage(
        "ReflectionAnalysis",
        "IL2091",
        Justification =
            "ConditionalWeakTable<TKey, TValue> is only referenced to reuse the callback delegate type, but no value is ever created through reflection.")]
    private TValue GetValueLocked(TKey key, ConditionalWeakTable<TKey, TValue>.CreateValueCallback createValueCallback)
    {
        // If we got here, the key was not in the table. Invoke the callback
        // (outside the lock) to generate the new value for the key.
        var newValue = createValueCallback(key);

        lock (lockObject)
        {
            // Now that we've taken the lock, must recheck in case we lost a race to add the key
            if (container.TryGetValueWorker(key, out var existingValue)) return existingValue;

            // Verified in-lock that we won the race to add the key. Add it now
            CreateEntry(key, newValue);

            return newValue;
        }
    }

    /// <inheritdoc />
    public Enumerator GetEnumerator()
    {
        // This is an optimization specific for this custom table that relies on the way the enumerator is being
        // used within the messenger type. Specifically, enumerators are always used in a using block, meaning
        // Dispose() is always guaranteed to be executed. Given we cannot remove the internal lock for the table
        // as it's needed to ensure consistency in case a container is resurrected (see below), the solution to
        // speedup iteration is to avoid taking and releasing a lock repeatedly every single time MoveNext() is
        // invoked. This is fine in this specific scenario because we're the only users of the enumerators so
        // there's no concern about blocking other threads while enumerating. So here we just preemptively take
        // a lock for the entire lifetime of the enumerator, and just release it once once we're done.
        Monitor.Enter(lockObject);

        return new Enumerator(this);
    }

    /// <summary>
    ///     Worker for adding a new key/value pair. Will resize the container if it is full.
    /// </summary>
    /// <param name="key">The key for the new entry.</param>
    /// <param name="value">The value for the new entry.</param>
    private void CreateEntry(TKey key, TValue value)
    {
        var container = this.container;

        if (!container.HasCapacity) this.container = container = container.Resize();

        container.CreateEntryNoResize(key, value);
    }

    /// <summary>
    ///     Provides an enumerator for the current <see cref="ConditionalWeakTable2{TKey, TValue}" /> instance.
    /// </summary>
    public ref struct Enumerator
    {
        /// <summary>
        ///     Parent table, set to null when disposed.
        /// </summary>
        private ConditionalWeakTable2<TKey, TValue> table;

        /// <summary>
        ///     Last index in the container that should be enumerated.
        /// </summary>
        private readonly int maxIndexInclusive;

        /// <summary>
        ///     The current index into the container.
        /// </summary>
        private int currentIndex;

        /// <summary>
        ///     The current key, if available.
        /// </summary>
        private TKey? key;

        /// <summary>
        ///     The current value, if available.
        /// </summary>
        private TValue? value;

        /// <summary>
        ///     Initializes a new instance of the <see cref="Enumerator" /> class.
        /// </summary>
        /// <param name="table">The input <see cref="ConditionalWeakTable2{TKey, TValue}" /> instance being enumerated.</param>
        public Enumerator(ConditionalWeakTable2<TKey, TValue> table)
        {
            // Store a reference to the parent table and increase its active enumerator count
            this.table = table;

            var container = table.container;

            if (container is null || container.FirstFreeEntry == 0)
                // The max index is the same as the current to prevent enumeration
                maxIndexInclusive = -1;
            else
                // Store the max index to be enumerated
                maxIndexInclusive = container.FirstFreeEntry - 1;

            currentIndex = -1;
            key = null;
            value = null;
        }

        /// <inheritdoc cref="IDisposable.Dispose" />
        public void Dispose()
        {
            // Release the lock
            Monitor.Exit(table.lockObject);

            table = null!;

            // Ensure we don't keep the last current alive unnecessarily
            key = null;
            value = null;
        }

        /// <inheritdoc cref="IEnumerator.MoveNext" />
        public bool MoveNext()
        {
            // From the table, we have to get the current container. This could have changed
            // since we grabbed the enumerator, but the index-to-pair mapping should not have
            // due to there being at least one active enumerator. If the table (or rather its
            // container at the time) has already been finalized, this will be null.
            var c = table.container;

            var currentIndex = this.currentIndex;
            var maxIndexInclusive = this.maxIndexInclusive;

            // We have the container. Find the next entry to return, if there is one. We need to loop as we
            // may try to get an entry that's already been removed or collected, in which case we try again.
            while (currentIndex < maxIndexInclusive)
            {
                currentIndex++;

                if (c.TryGetEntry(currentIndex, out key, out value))
                {
                    this.currentIndex = currentIndex;

                    return true;
                }
            }

            this.currentIndex = currentIndex;

            return false;
        }

        /// <summary>
        ///     Gets the current key.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TKey GetKey()
        {
            return key!;
        }

        /// <summary>
        ///     Gets the current value.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TValue GetValue()
        {
            return value!;
        }
    }

    /// <summary>
    ///     A single entry within a <see cref="ConditionalWeakTable2{TKey, TValue}" /> instance.
    /// </summary>
    private struct Entry
    {
        /// <summary>
        ///     Holds key and value using a weak reference for the key and a strong reference for the
        ///     value that is traversed only if the key is reachable without going through the value.
        /// </summary>
        public DependentHandle depHnd;

        /// <summary>
        ///     Cached copy of key's hashcode.
        /// </summary>
        public int HashCode;

        /// <summary>
        ///     Index of next entry, -1 if last.
        /// </summary>
        public int Next;
    }

    /// <summary>
    ///     Container holds the actual data for the table. A given instance of Container always has the same capacity. When we
    ///     need
    ///     more capacity, we create a new Container, copy the old one into the new one, and discard the old one. This helps
    ///     enable
    ///     lock-free reads from the table, as readers never need to deal with motion of entries due to rehashing.
    /// </summary>
    private sealed class Container
    {
        /// <summary>
        ///     The <see cref="ConditionalWeakTable2{TKey, TValue}" /> with which this container is associated.
        /// </summary>
        private readonly ConditionalWeakTable2<TKey, TValue> parent;

        /// <summary>
        ///     <c>buckets[hashcode &amp; (buckets.Length - 1)]</c> contains index of the first entry in bucket (-1 if empty).
        /// </summary>
        private int[] buckets;

        /// <summary>
        ///     The table entries containing the stored dependency handles
        /// </summary>
        private Entry[] entries;

        /// <summary>
        ///     Set to true when initially finalized
        /// </summary>
        private bool finalized;

        /// <summary>
        ///     <c>firstFreeEntry &lt; entries.Length => table</c> has capacity, entries grow from the bottom of the table.
        /// </summary>
        private int firstFreeEntry;

        /// <summary>
        ///     Flag detects if OOM or other background exception threw us out of the lock.
        /// </summary>
        private bool invalid;

        /// <summary>
        ///     Used to ensure the next allocated container isn't finalized until this one is GC'd.
        /// </summary>
        private volatile object? oldKeepAlive;

        /// <summary>
        ///     Initializes a new instance of the <see cref="Container" /> class.
        /// </summary>
        /// <param name="parent">
        ///     The input <see cref="ConditionalWeakTable2{TKey, TValue}" /> object associated with the current
        ///     instance.
        /// </param>
        internal Container(ConditionalWeakTable2<TKey, TValue> parent)
        {
            buckets = new int[InitialCapacity];

            for (var i = 0; i < buckets.Length; i++) buckets[i] = -1;

            entries = new Entry[InitialCapacity];

            // Only store the parent after all of the allocations have happened successfully.
            // Otherwise, as part of growing or clearing the container, we could end up allocating
            // a new Container that fails (OOMs) part way through construction but that gets finalized
            // and ends up clearing out some other container present in the associated CWT.
            this.parent = parent;
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="Container" /> class.
        /// </summary>
        /// <param name="parent">
        ///     The input <see cref="ConditionalWeakTable2{TKey, TValue}" /> object associated with the current
        ///     instance.
        /// </param>
        /// <param name="buckets">The array of buckets.</param>
        /// <param name="entries">The array of entries.</param>
        /// <param name="firstFreeEntry">The index of the first free entry.</param>
        private Container(ConditionalWeakTable2<TKey, TValue> parent, int[] buckets, Entry[] entries,
            int firstFreeEntry)
        {
            this.parent = parent;
            this.buckets = buckets;
            this.entries = entries;
            this.firstFreeEntry = firstFreeEntry;
        }

        /// <summary>
        ///     Gets the capacity of the current container.
        /// </summary>
        internal bool HasCapacity => firstFreeEntry < entries.Length;

        /// <summary>
        ///     Gets the index of the first free entry.
        /// </summary>
        internal int FirstFreeEntry => firstFreeEntry;

        /// <summary>
        ///     Worker for adding a new key/value pair. Container must NOT be full.
        /// </summary>
        internal void CreateEntryNoResize(TKey key, TValue value)
        {
            VerifyIntegrity();

            invalid = true;

            var hashCode = RuntimeHelpers.GetHashCode(key) & int.MaxValue;
            var newEntry = firstFreeEntry++;

            entries[newEntry].HashCode = hashCode;
            entries[newEntry].depHnd = new DependentHandle(key, value);

            var bucket = hashCode & (buckets.Length - 1);

            entries[newEntry].Next = buckets[bucket];

            // This write must be volatile, as we may be racing with concurrent readers. If they
            // see the new entry, they must also see all of the writes earlier in this method.
            Volatile.Write(ref buckets[bucket], newEntry);

            invalid = false;
        }

        /// <summary>
        ///     Worker for finding a key/value pair. Must hold lock.
        /// </summary>
        internal bool TryGetValueWorker(TKey key, [MaybeNullWhen(false)] out TValue value)
        {
            var entryIndex = FindEntry(key, out var secondary);

            value = Unsafe.As<TValue>(secondary);

            return entryIndex != -1;
        }

        /// <summary>
        ///     Returns -1 if not found (if key expires during FindEntry, this can be treated as "not found.").
        ///     Must hold lock, or be prepared to retry the search while holding lock.
        /// </summary>
        /// <remarks>This method requires <paramref name="value" /> to be on the stack to be properly tracked.</remarks>
        internal int FindEntry(TKey key, out object? value)
        {
            var hashCode = RuntimeHelpers.GetHashCode(key) & int.MaxValue;
            var bucket = hashCode & (buckets.Length - 1);

            for (var entriesIndex = Volatile.Read(ref buckets[bucket]);
                 entriesIndex != -1;
                 entriesIndex = entries[entriesIndex].Next)
                if (entries[entriesIndex].HashCode == hashCode)
                {
                    // if (_entries[entriesIndex].depHnd.UnsafeGetTargetAndDependent(out value) == key)
                    (var oKey, value) = entries[entriesIndex].depHnd.TargetAndDependent;

                    if (oKey == key)
                    {
                        // Ensure we don't get finalized while accessing DependentHandle
                        GC.KeepAlive(this);

                        return entriesIndex;
                    }
                }

            // Ensure we don't get finalized while accessing DependentHandle
            GC.KeepAlive(this);

            value = null;

            return -1;
        }

        /// <summary>
        ///     Gets the entry at the specified entry index.
        /// </summary>
        internal bool TryGetEntry(int index, [NotNullWhen(true)] out TKey? key, [MaybeNullWhen(false)] out TValue value)
        {
            if (index < entries.Length)
            {
                // object? oKey = entries[index].depHnd.UnsafeGetTargetAndDependent(out object? oValue);
                (var oKey, var oValue) = entries[index].depHnd.TargetAndDependent;

                // Ensure we don't get finalized while accessing DependentHandle
                GC.KeepAlive(this);

                if (oKey != null)
                {
                    key = Unsafe.As<TKey>(oKey);
                    value = Unsafe.As<TValue>(oValue)!;

                    return true;
                }
            }

            key = default;
            value = default;

            return false;
        }

        /// <summary>
        ///     Removes the specified key from the table, if it exists.
        /// </summary>
        internal bool Remove(TKey key)
        {
            VerifyIntegrity();

            var entryIndex = FindEntry(key, out _);

            if (entryIndex != -1)
            {
                RemoveIndex(entryIndex);

                return true;
            }

            return false;
        }

        /// <summary>
        ///     Removes a given entry at a specified index.
        /// </summary>
        /// <param name="entryIndex">The index of the entry to remove.</param>
        private void RemoveIndex(int entryIndex)
        {
            ref var entry = ref entries[entryIndex];

            // We do not free the handle here, as we may be racing with readers who already saw the hash code.
            // Instead, we simply overwrite the entry's hash code, so subsequent reads will ignore it.
            // The handle will be free'd in Container's finalizer, after the table is resized or discarded.
            Volatile.Write(ref entry.HashCode, -1);

            // Also, clear the key to allow GC to collect objects pointed to by the entry
            // entry.depHnd.UnsafeSetTargetToNull();
            entry.depHnd.Target = null;
        }

        /// <summary>
        ///     Resize, and scrub expired keys off bucket lists. Must hold <see cref="lockObject" />.
        /// </summary>
        /// <remarks>
        ///     <see cref="firstFreeEntry" /> is less than <c>entries.Length</c> on exit, that is, the table has at least one free
        ///     entry.
        /// </remarks>
        internal Container Resize()
        {
            var hasExpiredEntries = false;
            var newSize = buckets.Length;

            // If any expired or removed keys exist, we won't resize
            for (var entriesIndex = 0; entriesIndex < entries.Length; entriesIndex++)
            {
                ref var entry = ref entries[entriesIndex];

                if (entry.HashCode == -1)
                {
                    // the entry was removed
                    hasExpiredEntries = true;

                    break;
                }

                if (entry.depHnd.IsAllocated &&
                    // entry.depHnd.UnsafeGetTarget() is null)
                    entry.depHnd.Target is null)
                {
                    // the entry has expired
                    hasExpiredEntries = true;

                    break;
                }
            }

            if (!hasExpiredEntries)
                // Not necessary to check for overflow here, the attempt to allocate new arrays will throw
                newSize = buckets.Length * 2;

            return Resize(newSize);
        }

        /// <summary>
        ///     Creates a new <see cref="Container" /> of a specified size with the current items.
        /// </summary>
        /// <param name="newSize">The new requested size.</param>
        /// <returns>The new <see cref="Container" /> instance with the requested size.</returns>
        internal Container Resize(int newSize)
        {
            // Reallocate both buckets and entries and rebuild the bucket and entries from scratch.
            // This serves both to scrub entries with expired keys and to put the new entries in the proper bucket.
            var newBuckets = new int[newSize];

            for (var bucketIndex = 0; bucketIndex < newBuckets.Length; bucketIndex++) newBuckets[bucketIndex] = -1;

            var newEntries = new Entry[newSize];
            var newEntriesIndex = 0;

            // There are no active enumerators, which means we want to compact by removing expired/removed entries
            for (var entriesIndex = 0; entriesIndex < entries.Length; entriesIndex++)
            {
                ref var oldEntry = ref entries[entriesIndex];
                var hashCode = oldEntry.HashCode;
                var depHnd = oldEntry.depHnd;

                if (hashCode != -1 && depHnd.IsAllocated)
                {
                    // if (depHnd.UnsafeGetTarget() is not null)
                    if (depHnd.Target is not null)
                    {
                        ref var newEntry = ref newEntries[newEntriesIndex];

                        // Entry is used and has not expired. Link it into the appropriate bucket list
                        newEntry.HashCode = hashCode;
                        newEntry.depHnd = depHnd;

                        var bucket = hashCode & (newBuckets.Length - 1);

                        newEntry.Next = newBuckets[bucket];
                        newBuckets[bucket] = newEntriesIndex;
                        newEntriesIndex++;
                    }
                    else
                    {
                        // Pretend the item was removed, so that this container's finalizer will clean up this dependent handle
                        Volatile.Write(ref oldEntry.HashCode, -1);
                    }
                }
            }

            // Create the new container. We want to transfer the responsibility of freeing the handles from
            // the old container to the new container, and also ensure that the new container isn't finalized
            // while the old container may still be in use. As such, we store a reference from the old container
            // to the new one, which will keep the new container alive as long as the old one is.
            Container newContainer = new(parent!, newBuckets, newEntries, newEntriesIndex);

            // Once this is set, the old container's finalizer will not free transferred dependent handles
            oldKeepAlive = newContainer;

            // Ensure we don't get finalized while accessing DependentHandles
            GC.KeepAlive(this);

            return newContainer;
        }

        /// <summary>
        ///     Verifies that the current instance is valid.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if the current instance is invalid.</exception>
        private void VerifyIntegrity()
        {
            if (invalid)
            {
                static void Throw()
                {
                    throw new InvalidOperationException("The current collection is in a corrupted state.");
                }

                Throw();
            }
        }

        /// <summary>
        ///     Finalizes the current <see cref="Container" /> instance.
        /// </summary>
        ~Container()
        {
            // Skip doing anything if the container is invalid, including if somehow
            // the container object was allocated but its associated table never set.
            if (invalid || parent is null) return;

            // It's possible that the ConditionalWeakTable2 could have been resurrected, in which case code could
            // be accessing this Container as it's being finalized.  We don't support usage after finalization,
            // but we also don't want to potentially corrupt state by allowing dependency handles to be used as
            // or after they've been freed.  To avoid that, if it's at all possible that another thread has a
            // reference to this container via the CWT, we remove such a reference and then re-register for
            // finalization: the next time around, we can be sure that no references remain to this and we can
            // clean up the dependency handles without fear of corruption.
            if (!finalized)
            {
                finalized = true;

                lock (parent.lockObject)
                {
                    if (parent.container == this) parent.container = null!;
                }

                // Next time it's finalized, we'll be sure there are no remaining refs
                GC.ReRegisterForFinalize(this);

                return;
            }

            var entries = this.entries;

            invalid = true;
            this.entries = null!;
            buckets = null!;

            if (entries != null)
                for (var entriesIndex = 0; entriesIndex < entries.Length; entriesIndex++)
                    // We need to free handles in two cases:
                    // - If this container still owns the dependency handle (meaning ownership hasn't been transferred
                    //   to another container that replaced this one), then it should be freed.
                    // - If this container had the entry removed, then even if in general ownership was transferred to
                    //   another container, removed entries are not, therefore this container must free them.
                    if (oldKeepAlive is null || entries[entriesIndex].HashCode == -1)
                        entries[entriesIndex].depHnd.Dispose();
        }
    }
}

#endif