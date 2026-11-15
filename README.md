# Lyrava

[简体中文](README.zh-CN.md)

An open-source music client built with C# and .NET, starting with reliable everyday listening on iPhone and Bluetooth car stereos.

**Status: M0 engineering and feasibility validation.** The repository now contains a shared C# playback-state core, a bounded music-source probe and a minimal iOS MAUI media probe. There is no signed IPA or application release. The first-delivery features below remain targets; real-device and car acceptance are still open.

## First delivery

The first usable release, planned as **v0.1**, targets iPhone: music discovery, local playlists and favorites, a persistent playback queue, background and lock-screen playback, system media controls, Bluetooth car audio, interruption handling, and recovery from mobile-network changes. Distribution targets registered devices through Apple Ad Hoc signing.

The full roadmap retains account synchronization, offline downloads, synchronized lyrics, Android, Windows and macOS clients, advanced audio, recognition, social listening, and external integrations. CarPlay and platform-specific widgets have separate feasibility gates.

## Technical direction

- .NET 10 and .NET MAUI; application UI, business logic, and platform adapters authored in C#.
- Shared playback state and queue; platform-owned audio engines and media sessions.
- iOS playback through C# bindings to AVFoundation and MediaPlayer.
- A replaceable music-source adapter; YouTube/InnerTube compatibility is a separate technical risk.
- Local SQLite metadata, bounded media caching, and operating-system secure storage for credentials.
- No mandatory server in the initial architecture. A resolver service or non-C# runtime requires an explicit architecture decision before adoption.

MAUI's support lifecycle is separate from .NET's lifecycle. The current .NET MAUI 10 support deadline is 2027-05-11; SDK maintenance is included in the roadmap.

## Documentation

| Document | Purpose |
| --- | --- |
| [Roadmap](ROADMAP.md) | Milestones, delivery IDs, dependencies, acceptance and status |
| [Feature coverage](docs/feature-coverage.md) | Mapping from the assessed Metrolist scope to planned deliveries |
| [Architecture](docs/architecture.md) | Shared C# code, native platform boundaries, source and storage design |
| [Quality gates](docs/quality-gates.md) | Source, build, signing, device, car and daily-use evidence |
| [M0 evidence and readiness](docs/m0-readiness.md) | Current results, environment gaps, dependency inventory and the M1 gate |
| [iOS validation](docs/m0-ios-validation.md) | Media probe, Mac build instructions and device/car scenarios |
| [Source feasibility](docs/m0-source-feasibility.md) | Anonymous C# source experiments and unresolved playback requirements |
| [Architecture decision](docs/adr/0001-platform-and-distribution.md) | Language, platform, distribution and license decisions |
| [Contribution guide](CONTRIBUTING.md) | How to propose and deliver changes |
| [Handoff](HANDOFF.md) | Current state and the next implementation steps |
| [Changelog](CHANGELOG.md) | Actual changes only |

## Development status

The exact SDK is pinned in `global.json`. `Lyrava.slnx` contains the portable core, source adapter, command-line source probe and contract tests. `Lyrava.App.slnx` contains the iOS MAUI probe; it is a separate platform build so portable checks do not imply iOS acceptance.

Run the same bounded checks as the portable CI with PowerShell 7 or later:

```powershell
pwsh -NoProfile -File ./eng/verify.ps1
```

The script runs restore, a Release build, format verification and tests. Each step has a deadline, and the script stops on failure. See [quality gates](docs/quality-gates.md) for the exact commands and the separate platform checks.

The source probe reads public source data and reports counts, supported format and failure categories. It keeps temporary media addresses out of console output. A successful fixture or search request does not establish playable music, native seek or background playback; see [source feasibility](docs/m0-source-feasibility.md).

Normal iOS builds and signing require a compatible Mac/Xcode environment, even when application code is authored on Windows. Ad Hoc distribution requires registered device identifiers and valid signing assets; an IPA is not an unrestricted installer.

## License and provenance

Lyrava is licensed under **GPL-3.0-only**; see [LICENSE](LICENSE). Metrolist and InnerTubeX are technical references. The M0 code is newly authored and does not import their implementation code. Any later code reuse must preserve its original notices and compatible license terms; see [NOTICE](NOTICE) and the [dependency inventory](docs/m0-readiness.md).

Lyrava is an independent project and is not affiliated with YouTube or Google.
