using Lyrava.Core.Media;

namespace Lyrava.Core.Playback;

/// <summary>共享逻辑状态，不作为原生音频已播放的运行证据。</summary>
public sealed record PlaybackSnapshot(PlaybackStatus Status, MediaTrack? Track, TimeSpan Position);
