const assert = require('node:assert/strict');
const fs = require('node:fs');

// Inputs must be produced from the built release APK, not source manifests.
const [manifestPath, packagesPath] = process.argv.slice(2);
assert(manifestPath && packagesPath, 'Provide apkanalyzer manifest and defined-package output files');
const manifest = fs.readFileSync(manifestPath, 'utf8');
const packages = fs.readFileSync(packagesPath, 'utf8');
assert.match(manifest, /package="com\.musicserver\.shell"/);
assert.match(manifest, /android:usesCleartextTraffic="false"/);
assert.match(manifest, /android:allowBackup="false"/);
assert.doesNotMatch(manifest, /DiagnosticPlaybackService|diagnostic_network_security|android:networkSecurityConfig/);
assert.match(packages, /com\.musicserver\.shell\.MainActivity/);
assert.doesNotMatch(packages, /com\.musicserver\.shell\.Diagnostic|androidx\.media3/);
process.stdout.write('Built release APK excludes diagnostic classes, service, Media3, and cleartext allowance.\n');
