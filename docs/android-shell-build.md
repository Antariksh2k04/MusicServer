# Remote Android shell build

The manual **Android shell bootstrap** workflow generates the official Capacitor 8.5.2 Android project and a debug APK on GitHub Actions. Android Studio/JDK/SDK are optional on this PC for compilation. The shell only displays an installation-check screen; it has no sign-in, uploads, audio, or background service. The browser experiment continues through Vite.

## Publish and run

Use your normal, non-administrator PowerShell in the repository. Its local Git identity and `origin` are configured for your personal GitHub account; leave global GitLab credentials unchanged. Review `git status --short` before staging. Repository ignores exclude temporary media, tools, credentials, and signing keys.

```powershell
git add .
git diff --cached --stat
git diff --cached --check
git commit -m "Prepare local streaming experiment and Android shell bootstrap"
git push -u origin main
```

Before dispatch, check your account's **Settings → Billing and licensing** for available Actions minutes and storage, including usage by other repositories/Packages. Block paid usage; if a payment method exists, verify applicable budgets stop usage rather than merely notify. Do not run if this cannot be established. GitHub Free currently includes 2,000 minutes/month and 500 MB shared artifact storage; without a valid payment method, usage is blocked at quota. See [GitHub billing](https://docs.github.com/en/billing/concepts/product-billing/github-actions). The workflow's timeout and artifact cap do not enforce an account-wide ₹0 budget.

Open the repository's **Actions → Android shell bootstrap → Run workflow**, select `main`, and confirm the free-usage input only after that check. The default false input skips the build. There are no push/PR triggers, secrets, publishing, caches, or release signing. One standard Ubuntu job has a 20-minute timeout; a new dispatch cancels the preceding run. Download `android-shell-<run id>` within one day and delete it from GitHub after saving it. The upload is capped below 100 MiB.

## Install and record

Extract the downloaded artifact into an ignored directory such as `.local/android-shell-review/`. Compare the APK's SHA-256 with `SHA256SUMS.txt`:

```powershell
Get-FileHash .local/android-shell-review/music-server-shell-debug.apk -Algorithm SHA256
```

Transfer the APK to the CMF Phone 2 Pro, allow installation from that source temporarily, and open **Music Server Shell**. Record the workflow URL/commit, APK hash, exact Android/build version, installation result, and visible screen. Disable the install permission afterward. Different runs use temporary debug certificates; uninstall an old shell if an update is rejected. This clears its local data. This APK is not the release signing identity.

`bootstrap-source.tar.gz` contains the generated Android project, package manifest/lockfile, Capacitor config, and a report. Review it in the ignored directory, then incorporate the generated project and genuine dependencies under `src/web/` before native service work. Do not rerun `cap add android` over a customized project. Temporary build/local SDK paths and signing keys are excluded. Inspect workflow logs for exact Java/SDK/Gradle versions. Actual phone results remain separate from the report.

The next experiment needs local Platform Tools/USB debugging and `adb reverse` to reach the loopback backend. It will add Media3 and native authority; this shell cannot test seeking or background playback. Oracle remains in the final phase.
