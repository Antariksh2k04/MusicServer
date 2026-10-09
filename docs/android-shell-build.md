# Remote Android shell build

The manual **Android shell bootstrap** workflow builds the checked-in Capacitor 8.5.2 project at `src/web/android/` on GitHub Actions. Its initial generated sources and genuine npm lockfile were reviewed and incorporated from successful run `37897504459`. Future runs restore with `npm ci` and sync web assets into this existing project; they do not regenerate native sources. Android Studio/JDK/SDK are optional on this PC for compilation. The debug sources now contain the local native playback prototype; a new hosted build and physical validation are still required. The browser experiment continues through Vite.

## Publish and run

Use your normal, non-administrator PowerShell in the repository. Its local Git identity and `origin` are configured for your personal GitHub account; leave global GitLab credentials unchanged. Follow [gitworkflow.md](../gitworkflow.md): update `main` and create a task branch before editing. Review `git status --short` before staging. Repository ignores exclude temporary media, tools, credentials, and signing keys.

```powershell
git switch main
git pull --ff-only origin main
git switch -c feature/frontend/update-android-shell
# Make and validate the intended change before staging.
git add .
git diff --cached --stat
git diff --cached --check
git commit -m "feat(frontend): update Android shell"
git push -u origin feature/frontend/update-android-shell
```

Before dispatch, check your account's **Settings → Billing and licensing** for available Actions minutes and storage, including usage by other repositories/Packages. Block paid usage; if a payment method exists, verify applicable budgets stop usage rather than merely notify. Do not run if this cannot be established. GitHub Free currently includes 2,000 minutes/month and 500 MB shared artifact storage; without a valid payment method, usage is blocked at quota. See [GitHub billing](https://docs.github.com/en/billing/concepts/product-billing/github-actions). The workflow's timeout and artifact cap do not enforce an account-wide ₹0 budget.

Open the repository's **Actions → Android shell bootstrap → Run workflow**, select `main`, and confirm the free-usage input only after that check. The default false input skips the build. There are no push/PR triggers, secrets, publishing, caches, or release signing. One standard Ubuntu job has a 20-minute timeout; a new dispatch cancels the preceding run. Download `android-shell-<run id>` within one day and delete it from GitHub after saving it. The upload is capped below 100 MiB.

## Install and record

Extract the downloaded artifact into an ignored directory such as `.local/android-shell-review/`. Compare the APK's SHA-256 with `SHA256SUMS.txt`:

```powershell
Get-FileHash .local/android-shell-review/music-server-shell-debug.apk -Algorithm SHA256
```

Transfer the APK to the CMF Phone 2 Pro, allow installation from that source temporarily, and open **Music Server Shell**. Record the workflow URL/commit, APK hash, exact Android/build version, installation result, and visible screen. Disable the install permission afterward. Different runs use temporary debug certificates; uninstall an old shell if an update is rejected. This clears its local data. This APK is not the release signing identity.

The original bootstrap artifact's `bootstrap-source.tar.gz` is retained under `.local/android-shell-review/`. Its reviewed sources/locks now live under `src/web/`. Future artifacts contain the APK, `build-report.json`, and `SHA256SUMS.txt`; the commit in the report identifies their checked-in sources. Do not rerun `cap add android` over this project. Inspect workflow logs for exact Java/SDK/Gradle versions. Actual phone results remain separate from the report.

For optional local compilation after installing the pinned dependencies/JDK/SDK, run `npm --prefix src/web run android:sync`, then run `gradlew.bat assembleDebug` from `src/web/android/`. Sync regenerates plugin configuration and copied web assets while preserving app-owned Java/Kotlin sources. Generated web assets, plugin intermediates, local SDK paths, builds, and signing keys are ignored.

Open a pull request and merge it after review before selecting `main` for the build. To check a proposed change before merging, select its task branch for the manual workflow.

The workflow compiles both variants and debug instrumentation sources and invokes the native unit test task. The unused arithmetic template test has been removed; no app-specific native unit test cases exist yet, so `nativeUnitTestsVerified` remains false. It does not execute connected-device tests. Follow [Local Android playback](android-local-playback.md) for Platform Tools/USB setup, fixtures, native renewal, and the pending phone checks. Oracle remains in the final phase.
