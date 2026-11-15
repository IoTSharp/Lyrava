namespace Lyrava.App;

public sealed record MediaProbeView(
    string Intent,
    string NativeStatus,
    TimeSpan Position,
    TimeSpan? Duration,
    string Result);

public interface IMediaPlaybackService : IDisposable
{
    event EventHandler? Changed;

    MediaProbeView View { get; }

    Task LoadAndPlayAsync(string? address, CancellationToken cancellationToken);

    void StartSourceJourney(string? videoIds);

    void Resume();

    void Pause();

    Task SeekAsync(TimeSpan position, CancellationToken cancellationToken);

    void Stop();
}
