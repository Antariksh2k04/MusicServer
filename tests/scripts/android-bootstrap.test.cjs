const { test } = require('node:test');
const assert = require('node:assert/strict');
const { extendManifest, assertCapacitorLock, assertTemplate } = require('../../scripts/bootstrap-android.cjs');
const pins = require('../../scripts/android-toolchain.json');

test('bootstrap preserves locked web versions and adds only exact Capacitor dependencies', () => {
  const original = { scripts: { build: 'existing' }, dependencies: { react: '19.2.8' }, devDependencies: { vite: '8.3.0' } };
  const result = extendManifest(original, pins);
  assert.equal(result.dependencies.react, original.dependencies.react);
  assert.equal(result.devDependencies.vite, original.devDependencies.vite);
  assert.equal(result.dependencies['@capacitor/android'], pins.capacitor);
  assert.equal(result.dependencies['@capacitor/core'], pins.capacitor);
  assert.equal(result.devDependencies['@capacitor/cli'], pins.capacitor);
  assert.equal(original.dependencies['@capacitor/core'], undefined);
});

test('missing or substituted Capacitor lock entries stop generation', () => {
  const lock = { packages: Object.fromEntries(['core', 'android', 'cli'].map(name => [
    `node_modules/@capacitor/${name}`, { version: pins.capacitor, integrity: 'sha512-registry-value' },
  ])) };
  assert.doesNotThrow(() => assertCapacitorLock(lock, pins));
  lock.packages['node_modules/@capacitor/android'].version = '8.0.0';
  assert.throws(() => assertCapacitorLock(lock, pins), /android/);
  delete lock.packages['node_modules/@capacitor/android'];
  assert.throws(() => assertCapacitorLock(lock, pins), /android/);
});

test('a template version drift blocks building against unreviewed Android pins', () => {
  const template = { variables: 'ext { minSdkVersion = 24; compileSdkVersion = 36; targetSdkVersion = 36 }',
    build: "classpath 'com.android.tools.build:gradle:8.13.0'",
    wrapper: 'distributionUrl=https\\://services.gradle.org/distributions/gradle-8.14.3-all.zip' };
  assert.doesNotThrow(() => assertTemplate(template, pins));
  assert.throws(() => assertTemplate({ ...template, variables: template.variables.replace('compileSdkVersion = 36', 'compileSdkVersion = 37') }, pins), /compileSdk/);
  assert.throws(() => assertTemplate({ ...template, build: template.build.replace('8.13.0', '8.14.0') }, pins), /Plugin/);
  assert.throws(() => assertTemplate({ ...template, wrapper: template.wrapper.replace('8.14.3', '9.0.0') }, pins), /wrapper/);
});
