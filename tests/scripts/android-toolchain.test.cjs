const { execFileSync } = require('node:child_process');
const { mkdirSync, writeFileSync, unlinkSync, rmdirSync } = require('node:fs');
const { resolve, dirname, relative } = require('node:path');
const { test } = require('node:test');
const assert = require('node:assert/strict');

function run(jdk, sdk) {
  return JSON.parse(execFileSync('powershell.exe', ['-NoProfile', '-NonInteractive', '-File',
    resolve('scripts/check-android-toolchain.ps1'), '-JdkDirectory', jdk, '-SdkDirectory', sdk],
  { encoding: 'utf8', windowsHide: true }));
}

function fixture(t, { java = '21.0.8', api = '36', build = '35.0.0', adb = '37.0.1', compiler = true } = {}) {
  const base = resolve('.tools', 'android-inventory-tests', crypto.randomUUID());
  const directories = new Set();
  const files = [];
  function put(name, value = '') {
    const target = resolve(base, name);
    assert.ok(!relative(base, target).startsWith('..'));
    let parent = dirname(target);
    while (parent.startsWith(base)) { directories.add(parent); parent = dirname(parent); }
    mkdirSync(dirname(target), { recursive: true });
    writeFileSync(target, value);
    files.push(target);
  }
  put('jdk/release', `JAVA_VERSION="${java}"\n`);
  put('jdk/bin/java.exe');
  if (compiler) put('jdk/bin/javac.exe');
  put('sdk/platforms/android-36/source.properties', `AndroidVersion.ApiLevel=${api}\n`);
  put('sdk/platforms/android-36/android.jar');
  put('sdk/build-tools/35.0.0/source.properties', `Pkg.Revision=${build}\n`);
  for (const name of ['aapt2.exe', 'd8.bat', 'apksigner.bat']) put('sdk/build-tools/35.0.0/' + name);
  put('sdk/platform-tools/source.properties', `Pkg.Revision=${adb}\n`);
  put('sdk/platform-tools/adb.exe');
  t.after(() => {
    for (const file of files) unlinkSync(file);
    for (const directory of [...directories].sort((a, b) => b.length - a.length)) rmdirSync(directory);
  });
  return { jdk: resolve(base, 'jdk'), sdk: resolve(base, 'sdk') };
}

test('absent or network paths produce redacted blockers without probing a device', () => {
  const sentinel = 'private-toolchain-location-do-not-print';
  const report = run('\\\\' + sentinel + '\\jdk', resolve('.tools', sentinel));
  assert.equal(report.status, 'blocked');
  assert.equal(report.gateCompleted, false);
  assert.equal(report.nativeBuildVerified, false);
  assert.equal(report.physicalDeviceVerified, false);
  assert.equal(JSON.stringify(report).includes(sentinel), false);
});

test('matching SDK/JDK metadata never establishes a compiled APK or phone result', t => {
  const paths = fixture(t);
  const report = run(paths.jdk, paths.sdk);
  assert.equal(report.java.compilerMetadataMatches, true);
  assert.equal(report.sdk.platformMetadataMatches, true);
  assert.equal(report.sdk.buildToolsMetadataMatches, true);
  assert.equal(report.sdk.adbMetadataMatches, true);
  assert.equal(report.gateCompleted, false);
  assert.equal(report.nativeBuildVerified, false);
  assert.equal(report.physicalDeviceVerified, false);
});

for (const [options, field] of [
  [{ java: '17.0.16' }, 'jdk21Compiler'],
  [{ compiler: false }, 'jdk21Compiler'],
  [{ api: '35' }, 'android36Platform'],
  [{ build: '36.0.0' }, 'buildTools35'],
  [{ adb: '36.0.2' }, 'platformTools37'],
]) {
  test('rejects incomplete or mismatched tool metadata: ' + JSON.stringify(options), t => {
    const paths = fixture(t, options);
    const report = run(paths.jdk, paths.sdk);
    assert.equal(report.status, 'blocked');
    assert.ok(report.missing.includes(field));
  });
}
