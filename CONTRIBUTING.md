# Contributing to Lyrava

Lyrava is currently establishing its implementation baseline. Read [ROADMAP](ROADMAP.md), [architecture](docs/architecture.md) and [quality gates](docs/quality-gates.md) before proposing a change.

- Reference one roadmap delivery ID or a concrete independent defect.
- Keep application code in C#. Changes to language/runtime dependencies, mandatory servers, supported platforms or distribution require an architecture decision.
- Preserve unrelated local changes. Use focused commits and explain observable behavior, validation and remaining limits in a pull request.
- Do not claim platform or car compatibility based on mocks, a simulator or a successful build.
- Do not include account cookies, tokens, signed media URLs, private signing keys, provisioning profiles, UDIDs or personal listening data in code, issues or diagnostic attachments.
- Preserve upstream copyright and license notices when proposing imported or translated code. Document the source revision and license.
- Contributions to newly authored project files are under GPL-3.0-only, consistent with [LICENSE](LICENSE).

There is no application build command yet. The actual .NET solution and corresponding checks are part of `M0-02`; this document must be updated when those are introduced.
