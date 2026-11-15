# Contributing to Lyrava

Lyrava is currently establishing its implementation baseline. Read [ROADMAP](ROADMAP.md), [architecture](docs/architecture.md) and [quality gates](docs/quality-gates.md) before proposing a change.

- Reference one roadmap delivery ID or a concrete independent defect.
- Keep application code in C#. Changes to language/runtime dependencies, mandatory servers, supported platforms or distribution require an architecture decision.
- Preserve unrelated local changes. Use focused commits and explain observable behavior, validation and remaining limits in a pull request.
- Do not claim platform or car compatibility based on mocks, a simulator or a successful build.
- Do not include account cookies, tokens, signed media URLs, private signing keys, provisioning profiles, UDIDs or personal listening data in code, issues or diagnostic attachments.
- Preserve upstream copyright and license notices when proposing imported or translated code. Document the source revision and license.
- Contributions to newly authored project files are under GPL-3.0-only, consistent with [LICENSE](LICENSE).

For `M0-02`, use SDK `10.0.404` and PowerShell 7+ to run `pwsh -NoProfile -File ./eng/verify.ps1`. This is the same restore, Release build, format and test sequence used by the portable CI. Do not run formatting that rewrites other contributors' files.

The iOS MAUI probe is in `Lyrava.App.slnx`; follow [iOS validation](docs/m0-ios-validation.md) for platform commands. Portable tests and Windows managed compilation cannot close iOS build, signing, phone or car gates. Current results and inputs needed to enter M1 are tracked in [M0 readiness](docs/m0-readiness.md).
