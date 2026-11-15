using Lyrava.Core.Media;
using Lyrava.Core.Playback;

namespace Lyrava.Core.Tests;

public sealed class PlaybackSessionStateTests
{
    private static readonly MediaTrack Track = new("probe-track", "Probe track", duration: TimeSpan.FromMinutes(3));

    [Fact]
    public void Load_RestoredPosition_DoesNotStartPlayback()
    {
        var session = new PlaybackSessionState();
        Assert.Equal(PlaybackStatus.Empty, session.Snapshot.Status);

        session.Load(Track, TimeSpan.FromSeconds(30));

        Assert.Equal(PlaybackStatus.Ready, session.Snapshot.Status);
        Assert.Equal(TimeSpan.FromSeconds(30), session.Snapshot.Position);
    }

    [Fact]
    public void Pause_DuringInterruption_PreventsAutomaticResume()
    {
        var session = PlayingSession();
        session.BeginInterruption();
        session.Pause();

        session.EndInterruption(systemAllowsResume: true);

        Assert.Equal(PlaybackStatus.Paused, session.Snapshot.Status);
    }

    [Theory]
    [InlineData(true, PlaybackStatus.Playing)]
    [InlineData(false, PlaybackStatus.Paused)]
    public void EndInterruption_RequiresSystemPermission(bool systemAllowsResume, PlaybackStatus expected)
    {
        var session = PlayingSession();
        session.BeginInterruption();

        session.EndInterruption(systemAllowsResume);

        Assert.Equal(expected, session.Snapshot.Status);
    }

    [Fact]
    public void BeginInterruption_AlreadyPaused_DoesNotAcquireResumeIntent()
    {
        var session = PlayingSession();
        session.Pause();
        session.BeginInterruption();

        session.EndInterruption(systemAllowsResume: true);

        Assert.Equal(PlaybackStatus.Paused, session.Snapshot.Status);
    }

    [Fact]
    public void RouteDisconnected_DuringInterruption_PreventsSpeakerPlayback()
    {
        var session = PlayingSession();
        session.BeginInterruption();
        session.RouteDisconnected();

        session.EndInterruption(systemAllowsResume: true);

        Assert.Equal(PlaybackStatus.Paused, session.Snapshot.Status);
    }

    [Fact]
    public void Stop_DuringInterruption_RemovesTrackAndResumeIntent()
    {
        var session = PlayingSession();
        session.UpdatePosition(TimeSpan.FromSeconds(40));
        session.BeginInterruption();
        session.Stop();

        session.EndInterruption(systemAllowsResume: true);

        Assert.Equal(new(PlaybackStatus.Empty, null, TimeSpan.Zero), session.Snapshot);
        Assert.Throws<InvalidOperationException>(session.Play);
    }

    [Fact]
    public void Fail_ThenInterruptionEnds_DoesNotResume()
    {
        var session = PlayingSession();
        session.BeginInterruption();
        session.Fail();

        session.EndInterruption(systemAllowsResume: true);

        Assert.Equal(PlaybackStatus.Failed, session.Snapshot.Status);
        Assert.Throws<InvalidOperationException>(session.Play);
    }

    [Fact]
    public void Load_NewTrack_ClearsPreviousResumeIntent()
    {
        var session = PlayingSession();
        session.BeginInterruption();
        var nextTrack = new MediaTrack("next-track", "Next track");
        session.Load(nextTrack);

        session.EndInterruption(systemAllowsResume: true);

        Assert.Equal(PlaybackStatus.Ready, session.Snapshot.Status);
        Assert.Same(nextTrack, session.Snapshot.Track);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(181)]
    public void UpdatePosition_OutsideTrackBounds_RejectsPosition(int seconds)
    {
        var session = PlayingSession();

        Assert.Throws<ArgumentOutOfRangeException>(() => session.UpdatePosition(TimeSpan.FromSeconds(seconds)));

        Assert.Equal(TimeSpan.Zero, session.Snapshot.Position);
    }

    private static PlaybackSessionState PlayingSession()
    {
        var session = new PlaybackSessionState();
        session.Load(Track);
        session.Play();
        return session;
    }
}
