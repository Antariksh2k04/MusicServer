# Android shell bootstrap preparation — 2026-10-09

## Scope and state

The owner selected GitHub Actions remote builds and provided the personal GitHub repository. After the owner repaired the `.git` directory ownership, sandbox commands resumed. Read-only checks confirmed repository-local personal author/email configuration and the requested `origin`; no global GitLab identity or credential was changed. This session did not commit, push, dispatch a workflow, produce an APK, install on a phone, or complete tasks 1.4/1.5.

Local npm registry access still fails with `EACCES`. The bootstrap therefore runs the pinned official Capacitor CLI on a Linux runner in an isolated ignored workspace. It extends the existing web manifest, asks npm for a genuine lockfile, restores with `npm ci`, verifies exact Capacitor entries and native template pins, and builds only empty-shell assets for Android. The browser entry remains separate. The returned native sources/locks must be reviewed and incorporated before task 2.8.

## Executed local validation

- `node --check scripts/bootstrap-android.cjs`: passed.
- `node --test tests/scripts/android-bootstrap.test.cjs`: 3 passed, covering preserved web pins and rejection of missing/substituted Capacitor locks or native template drift.
- `node --test tests/scripts/android-toolchain.test.cjs tests/scripts/prerequisites.test.cjs tests/scripts/android-bootstrap.test.cjs`: 20 passed in the combined script suite.
- `npm --prefix src/web test`: 20 passed.
- `npm --prefix src/web run build:native-shell`: passed TypeScript/build; empty-shell main bundle approximately 1.27 MB minified / 278 kB gzip.
- `npm --prefix src/web run build`: passed; browser bundle approximately 1.29 MB / 282 kB gzip. Both builds retain the existing bundle-size warning.
- `openspec validate first-music-stream --strict`: passed after the build-location revision.
- Workflow YAML parsed without duplicate-key errors using the installed OpenSpec YAML dependency; its only trigger is `workflow_dispatch`. Inspection of built shell assets found the installation screen and no diagnostic API path. These are local checks, not GitHub workflow execution.

These checks do not verify npm/Gradle restore on Linux, native compilation, artifact delivery, audible playback, or device behavior. Backend sources were untouched; backend tests were not rerun for this preparation.

## Workflow review and pending evidence

Self-review checked manual-only dispatch, default-false free-usage acknowledgement, read-only token/checkout without persisted credentials, immutable official-action revisions, no cache or production secrets, a standard Ubuntu runner, 20-minute timeout, one-day artifact retention, and an upload under 100 MiB. Account allowances/paid-usage blocking still need the owner's actual billing settings; the workflow cannot verify them. APK signing is temporary debug signing; no key is uploaded. Source archives exclude Gradle build/cache and local SDK paths. Generated credentials/private media are never inputs.

Official source checks confirmed the [Capacitor 8.5.2 template](https://github.com/ionic-team/capacitor/tree/8.5.2/android-template) SDK/AGP/Gradle pins and [CLI entry](https://github.com/ionic-team/capacitor/blob/8.5.2/cli/package.json). The [runner image inventory](https://github.com/actions/runner-images/blob/main/images/ubuntu/Ubuntu2404-Readme.md) establishes candidate tools, not evidence of a run; the workflow installs the selected platform/build tools and logs actual versions. [GitHub billing](https://docs.github.com/en/billing/concepts/product-billing/github-actions) governs account quota and paid usage.

Next: publish/run using `docs/android-shell-build.md`; save the run URL/commit, actual dependency/tool versions, APK hash, reviewed native sources/lockfile, and physical installation/launch evidence. Then implement task 2.8 with local USB-to-loopback access and native Media3/authentication. Cloud, Google, MySQL, full toolchain/host checks, and physical background/seek acceptance remain open.
