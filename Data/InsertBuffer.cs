using System.Collections.Concurrent;

namespace Circles.Index.Data;

public class InsertBuffer<T>
{
    private ConcurrentQueue<T> _currentSegment = new();

    public void Add(T item)
    {
        _currentSegment.Enqueue(item);
    }

    public IEnumerable<T> TakeSnapshot()
    {
        var snapshotSegment = Interlocked.Exchange(ref _currentSegment, new ConcurrentQueue<T>());
        return snapshotSegment.ToList();
    }
}