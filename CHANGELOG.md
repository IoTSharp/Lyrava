# Changelog

## Unreleased

### Added

- Lyrava product identity and bilingual project introduction.
- Full delivery roadmap, with the first usable iPhone car-audio release as v0.1.
- Feature coverage, C# / MAUI architecture, distribution/license decision, and separate verification gates.
- Contributor, agent and handoff documentation.
- M0-02: .NET SDK 10.0.404, portable solution, shared playback-intent contracts and tests, centralized build rules, and a bounded verification script shared with the portable CI configuration.
- M0-03/M0-04: a C# iOS MAUI media probe with application-owned AVPlayer, background-audio configuration, media controls, interruption handling and resource cleanup; device and signing acceptance remain open.
- M0-05: a C# anonymous music-source adapter and command-line feasibility probe with bounded requests, format/expiry/Range checks, redacted output and protocol tests. Complete real-source playback is unverified.
- M0-06: evidence, dependency and environment records that retain the unmet gate before M1.
- M0 supervision: refreshed iOS acceptance receipts, source feasibility and hosted CI evidence; the M1 gate remains closed.
- Windows-first slice: multi-targeted `Lyrava.App`, added the platform-neutral playback service contract, Windows MAUI startup, and a bounded `Windows.Media.Playback.MediaPlayer` implementation. The current build is unpackaged and has no runtime or MSIX acceptance yet.
- M0-02/M0-06 Windows control-plane slice: wired `SystemMediaTransportControls` playback buttons, metadata, timeline updates and bounded position requests into the single Windows playback service. System-panel and release runtime evidence remains unrun.

No application features or installable packages have been released.
