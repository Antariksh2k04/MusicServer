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

## Hosted-run SDK discovery correction

The owner reported the first hosted attempt failed at `sdkmanager --install` with `sdkmanager: command not found`. That is a failed runner check, not an APK build result. The workflow now resolves the executable beneath `ANDROID_HOME`, preferring `cmdline-tools/latest/bin/sdkmanager` and falling back to an executable in a versioned Command-line Tools directory. Missing tools produce an explicit error; SDK installation failures retain their exit status. Requested API 36/Build Tools 35.0.0, manual dispatch, permissions, timeout, and budget acknowledgement are preserved.

`node --test tests/scripts/android-sdk-workflow.test.cjs tests/scripts/android-bootstrap.test.cjs` passed all 7 checks. The four new checks execute the actual workflow Bash block with an isolated fake SDK, rejecting unqualified PATH lookup. They cover latest/versioned tools, paths containing spaces, missing tools, and installer failure. Git Bash was used locally; fixtures establish discovery/error handling, not actual Android package installation. Workflow YAML parsing, `git diff --check`, and strict OpenSpec validation passed. Self-review verified that the fix changes SDK discovery without broadening workflow triggers or dropping native acceptance. No hosted retry, APK, or physical-device check was performed here; tasks 1.4/1.5 remain unchecked.

Publish the workflow and regression test together, then start a new dispatch on the updated `main`. Rerunning the original failed run uses its original commit and does not exercise this fix.

## Owner-reported successful workflow

After the SDK discovery correction, the owner reported that the action ran successfully. The run URL/commit, job completion (rather than a skipped job), artifact contents, actual dependency/tool versions, and APK hash have not yet been inspected in this workspace. Record this as an owner-reported workflow result; do not infer physical installation, background playback, or completed task 1.4/1.5 from it.

Next retrieve `android-shell-<run id>` before its one-day expiry, extract it under `.local/android-shell-review/`, verify the APK hash against `SHA256SUMS.txt`, and install/launch the shell on the CMF Phone 2 Pro. Retain `bootstrap-source.tar.gz` for native-source/lockfile review and incorporation. Record the run URL and exact phone Android/build version with the installation result before progressing to task 2.8.

## Owner-reported installation and launch

After the installation-check instructions, the owner confirmed the shell is working on the phone. Record physical installation/launch as owner-reported successful. This confirms the empty shell only; no audio service or background-playback result is implied.

The downloaded artifact is not yet present at `.local/android-shell-review/`, and `src/web/android/` does not exist in this workspace. Native-source/lockfile review and incorporation, run/commit reference, APK hash, and exact phone OS/build are still pending. Task 1.5 remains unchecked until the remaining evidence and generated-source review are complete; the next implementation work is the task 2.8 Media3/native authority experiment.

## Retrieved artifact and reviewed source incorporation

The owner's Downloads contained the ZIP and extracted artifact for [run 37897504459](https://github.com/Antariksh2k04/MusicServer/actions/runs/37897504459). Copied the APK, source archive, and checksum manifest into ignored `.local/android-shell-review/`, leaving the originals intact. Both files match the supplied SHA-256 manifest:

| Artifact | SHA-256 |
| --- | --- |
| `music-server-shell-debug.apk` | `43170c23adc958cc2f44e40929d092fb37f01d582e2eaf8686336171f2e4b7a3` |
| `bootstrap-source.tar.gz` | `4bcf681ea28cf66bc3c14e9e36f48a1ccbb277bc5c1d1a16155ac6513110c34b` |

Checked all 126 archive entries for relative allowed paths and rejected links/special entry types before extraction. The returned report names commit `55eb3722c74a9fd111c194825f58109ccc19bb99`, Node `v24.18.0`, verified template generation, and successful APK compilation. Its device/background flags remain false; the owner's separate installation/launch confirmation is the physical shell result. The report records selected pins, not observed Java patch/IDE/phone versions.

Reviewed the existing manifest versions/scripts against the returned manifest, exact Capacitor core/Android/CLI 8.5.2 lock entries/integrities, SDK 24/36/36, AGP 8.13.0, Gradle 8.14.3, Java 21 compilation configuration, app manifest/MainActivity, relative Capacitor module references, and ignore rules. Incorporated the generated project under `src/web/android/` and the genuine npm manifest/lockfile. The official generated Gradle wrapper JAR is retained; its SHA-256 is `7d3a4ac4de1c32b59bc6a4eb8ecb8e612ccd0cf1ae1e99f66902da64df296172` (observed hash, not an independently fetched upstream comparison). APKs, copied web assets, plugin intermediates, SDK paths, signing keys, and archive files remain ignored.

Future manual workflow runs use `npm ci`, frontend checks, `npm run android:sync`, and the checked-in project's `assembleDebug`. They no longer invoke the temporary bootstrap or `cap add android`. New artifacts carry the APK and a commit/run build report rather than another generated source archive. Actual hosted execution of this revised source-build workflow remains pending; the successful initial bootstrap and owner-installed APK are the shell evidence.

Local validation after incorporation: 20 frontend tests and 7 workflow/bootstrap tests passed; native-shell and browser TypeScript/Vite builds passed with the existing large-bundle warning. Backend/test project smoke build passed with zero warnings/errors using workspace .NET/NuGet caches and `.tools/native-shell-check-bin` as `BaseOutputPath` (actual PowerShell-forwarded output `.tools/native-shell-check-binDebug/net10.0`). Backend tests were not rerun because server sources were unchanged. Workflow YAML and every run block's Bash syntax passed. The initial ad-hoc YAML-check helper assumed every step had a name; corrected the helper and reran successfully without changing the workflow for that helper error.

Self-review confirmed canonical native code is preserved across sync/build, source/lock provenance matches the working APK, private/temporary data remains ignored, and no playback or production integration is inferred. Task 1.5's minimal structure/build/install/source-review criteria are now supported; tasks 1.3/1.4 retain exact device/full toolchain dependencies, and task 2.8 remains the next native playback implementation. Do not treat the empty shell as native audio or completed capability acceptance.

## Instrumentation review correction

Review identified that the imported template instrumentation assertion expected `com.getcapacitor.app` instead of the configured application ID. Updated it to `com.musicserver.shell` and verified the expected string matches `app/build.gradle`. `connectedAndroidTest` was not run; this static check does not establish a passing device instrumentation suite.
