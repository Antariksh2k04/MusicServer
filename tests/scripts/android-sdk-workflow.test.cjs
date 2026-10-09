const { test } = require('node:test');
const assert = require('node:assert/strict');
const { spawnSync } = require('node:child_process');
const { chmodSync, existsSync, mkdirSync, mkdtempSync, readFileSync, rmdirSync, unlinkSync, writeFileSync } = require('node:fs');
const { dirname, resolve } = require('node:path');

const bash = process.platform === 'win32' ? 'C:/Program Files/Git/bin/bash.exe' : 'bash';
const workflow = readFileSync(resolve('.github/workflows/android-shell.yml'), 'utf8').replace(/\r\n/g, '\n');
const sdkStep = workflow.match(/      - name: Install selected Android SDK components\n        shell: bash\n        run: \|\n([\s\S]*?)(?=      - name:)/);
assert.ok(sdkStep, 'SDK install step must remain an executable Bash block');
const commands = sdkStep[1].replace(/^          /gm, '');

function fixture(t, managerVersion) {
  const parent = resolve('.tools/android-sdk-workflow-tests');
  mkdirSync(parent, { recursive: true });
  const root = mkdtempSync(resolve(parent, 'sdk with spaces-'));
  const files = [];
  const directories = new Set([root]);
  const record = resolve(root, 'sdk-arguments.txt');
  function put(name, text) {
    const path = resolve(root, name);
    for (let folder = dirname(path); folder !== root; folder = dirname(folder)) directories.add(folder);
    mkdirSync(dirname(path), { recursive: true });
    writeFileSync(path, text);
    files.push(path);
    return path;
  }
  if (managerVersion) {
    const manager = put(`cmdline-tools/${managerVersion}/bin/sdkmanager`,
      '#!/usr/bin/env bash\nprintf "%s\\n" "$@" > "$SDK_TEST_RECORD"\nexit "${SDK_TEST_EXIT:-0}"\n');
    chmodSync(manager, 0o755);
  }
  put('platforms/android-36/source.properties', 'AndroidVersion.ApiLevel=36\n');
  put('build-tools/35.0.0/source.properties', 'Pkg.Revision=35.0.0\n');
  t.after(() => {
    if (existsSync(record)) unlinkSync(record);
    for (const file of files) unlinkSync(file);
    for (const directory of [...directories].sort((a, b) => b.length - a.length)) rmdirSync(directory);
  });
  return { root: root.replace(/\\/g, '/'), record };
}

function run(paths, extraEnv = {}) {
  // Reject PATH lookup; exercise real Bash discovery and the absolute SDK tool invocation.
  const result = spawnSync(bash, ['--noprofile', '--norc', '-eo', 'pipefail', '-c',
    'java() { :; }; node() { :; }; npm() { :; }; sdkmanager() { return 99; };\n' + commands], {
    encoding: 'utf8', windowsHide: true,
    env: { ...process.env, ANDROID_HOME: paths.root, SDK_TEST_RECORD: paths.record.replace(/\\/g, '/'), SDK_TEST_EXIT: '0', ...extraEnv },
  });
  assert.ifError(result.error);
  return result;
}

for (const version of ['latest', '12.0']) {
  test(`SDK installation works without sdkmanager on PATH using ${version} tools and a spaced path`, t => {
    const paths = fixture(t, version);
    const result = run(paths);
    assert.equal(result.status, 0, result.stderr);
    assert.deepEqual(readFileSync(paths.record, 'utf8').trimEnd().split('\n'), [
      `--sdk_root=${paths.root}`, '--install', 'platforms;android-36', 'build-tools;35.0.0',
    ]);
  });
}

test('missing command-line tools stop the step with an actionable error', t => {
  const paths = fixture(t);
  const result = run(paths);
  assert.equal(result.status, 1, result.stderr);
  assert.match(result.stdout, /::error::Android SDK Command-line Tools not found/);
  assert.equal(existsSync(paths.record), false);
});

test('SDK installation failure propagates rather than continuing to build', t => {
  const paths = fixture(t, 'latest');
  const result = run(paths, { SDK_TEST_EXIT: '7' });
  assert.equal(result.status, 7, result.stderr);
});
