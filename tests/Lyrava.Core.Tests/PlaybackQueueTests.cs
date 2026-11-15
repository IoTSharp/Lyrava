using Lyrava.Core.Media;
using Lyrava.Core.Playback;

namespace Lyrava.Core.Tests;

public sealed class PlaybackQueueTests
{
    private static readonly MediaTrack First = new("first", "First");
    private static readonly MediaTrack Second = new("second", "Second");
    private static readonly MediaTrack Third = new("third", "Third");

    [Fact]
    public void Enqueue_FirstTrackBecomesCurrent_AndDuplicateTrackIsAllowed()
    {
        var queue = new PlaybackQueue();

        Assert.Equal(0, queue.Enqueue(First));
        Assert.Equal(1, queue.Enqueue(First));

        Assert.Equal(2, queue.Count);
        Assert.Equal(0, queue.CurrentIndex);
        Assert.Same(First, queue.Current);
    }

    [Fact]
    public void MoveNextAndPrevious_StopAtQueueBoundaries()
    {
        var queue = QueueWithThreeTracks();

        Assert.False(queue.MovePrevious());
        Assert.True(queue.MoveNext());
        Assert.Same(Second, queue.Current);
        Assert.True(queue.MoveNext());
        Assert.Same(Third, queue.Current);
        Assert.False(queue.MoveNext());
        Assert.True(queue.MovePrevious());
        Assert.Same(Second, queue.Current);
    }

    [Fact]
    public void InsertAndMove_PreserveCurrentTrackIdentity()
    {
        var queue = QueueWithThreeTracks();
        queue.Select(1);

        queue.Insert(0, new MediaTrack("inserted", "Inserted"));
        Assert.Equal(2, queue.CurrentIndex);
        Assert.Same(Second, queue.Current);

        Assert.True(queue.Move(2, 1));
        Assert.Equal(1, queue.CurrentIndex);
        Assert.Same(Second, queue.Current);
        Assert.Equal(new[] { "inserted", "second", "first", "third" }, queue.Items.Select(item => item.Id));
    }

    [Fact]
    public void RemoveCurrent_SelectsFollowingTrack_ThenPreviousAtEnd()
    {
        var queue = QueueWithThreeTracks();
        queue.Select(1);

        Assert.True(queue.RemoveAt(1));
        Assert.Same(Third, queue.Current);
        Assert.Equal(1, queue.CurrentIndex);

        Assert.True(queue.RemoveAt(1));
        Assert.Same(First, queue.Current);
        Assert.Equal(0, queue.CurrentIndex);
    }

    [Fact]
    public void InvalidSelectionRemovalAndMove_AreIdempotent()
    {
        var queue = QueueWithThreeTracks();
        var original = queue.Items;

        Assert.False(queue.Select(-1));
        Assert.False(queue.Select(3));
        Assert.False(queue.RemoveAt(-1));
        Assert.False(queue.RemoveAt(3));
        Assert.False(queue.Move(0, 3));
        Assert.Equal(original, queue.Items);
        Assert.Same(First, queue.Current);
    }

    [Fact]
    public void Clear_CanBeRepeated_AndResetsCurrent()
    {
        var queue = QueueWithThreeTracks();

        queue.Clear();
        queue.Clear();

        Assert.Equal(0, queue.Count);
        Assert.Equal(-1, queue.CurrentIndex);
        Assert.Null(queue.Current);
        Assert.False(queue.MoveNext());
        Assert.False(queue.MovePrevious());
    }

    [Fact]
    public void InsertOutOfRange_ThrowsWithoutChangingQueue()
    {
        var queue = QueueWithThreeTracks();

        Assert.Throws<ArgumentOutOfRangeException>(() => queue.Insert(4, First));

        Assert.Equal(new[] { "first", "second", "third" }, queue.Items.Select(item => item.Id));
        Assert.Same(First, queue.Current);
    }

    private static PlaybackQueue QueueWithThreeTracks()
    {
        var queue = new PlaybackQueue();
        queue.Enqueue(First);
        queue.Enqueue(Second);
        queue.Enqueue(Third);
        return queue;
    }
}
