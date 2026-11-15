using Lyrava.Core.Storage;

namespace Lyrava.Core.Tests;

public sealed class PlaybackStateSnapshotTests
{
    [Fact]
    public async Task InMemoryStore_RoundTripsMetadataAndExplicitPause()
    {
        var track = new PlaybackQueueItem("track-1", "Track 1", "Artist", TimeSpan.FromMinutes(3));
        var snapshot = PlaybackStateSnapshot.Create([track], currentIndex: 0, position: TimeSpan.FromSeconds(12), explicitlyPaused: true);
        var store = new InMemoryPlaybackStateStore();

        await store.SaveAsync(snapshot);
        var loaded = await store.LoadAsync();

        Assert.Same(snapshot, loaded);
        Assert.True(loaded!.ExplicitlyPaused);
        Assert.False(loaded.ResumeAllowed);
        Assert.Equal("track-1", loaded.Queue[0].Id);
        Assert.Equal(TimeSpan.FromSeconds(12), loaded.Position);
    }

    [Fact]
    public void EmptySnapshot_DefaultsToNoAutoplay()
    {
        var snapshot = PlaybackStateSnapshot.Empty();

        Assert.Equal(PlaybackStateSnapshot.CurrentVersion, snapshot.Version);
        Assert.Empty(snapshot.Queue);
        Assert.Equal(-1, snapshot.CurrentIndex);
        Assert.Equal(TimeSpan.Zero, snapshot.Position);
        Assert.False(snapshot.ExplicitlyPaused);
        Assert.False(snapshot.ResumeAllowed);
    }

    [Fact]
    public void InvalidState_IsRejectedBeforePersistence()
    {
        var track = new PlaybackQueueItem("track-1", "Track 1", duration: TimeSpan.FromSeconds(10));

        Assert.Throws<ArgumentOutOfRangeException>(() => PlaybackStateSnapshot.Create([track], currentIndex: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => PlaybackStateSnapshot.Create([track], currentIndex: 0, position: TimeSpan.FromSeconds(11)));
        Assert.Throws<ArgumentException>(() => PlaybackStateSnapshot.Create([track], currentIndex: 0, explicitlyPaused: true, resumeAllowed: true));
        Assert.Throws<ArgumentOutOfRangeException>(() => PlaybackStateSnapshot.Create(Array.Empty<PlaybackQueueItem>(), currentIndex: 0));
    }

    [Fact]
    public async Task Store_HonorsCancellation_AndDoesNotChangeExistingSnapshot()
    {
        var store = new InMemoryPlaybackStateStore();
        var original = PlaybackStateSnapshot.Empty();
        await store.SaveAsync(original);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () => await store.LoadAsync(cancellation.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await store.SaveAsync(PlaybackStateSnapshot.Empty(), cancellation.Token));
        Assert.Same(original, await store.LoadAsync());
    }
}
