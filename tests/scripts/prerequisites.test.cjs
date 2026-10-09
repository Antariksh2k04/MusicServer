const { execFileSync } = require('node:child_process');
const { resolve } = require('node:path');
const { test } = require('node:test');
const assert = require('node:assert/strict');

function run(script, args = [], env = {}) {
  return JSON.parse(execFileSync('powershell.exe', ['-NoProfile', '-NonInteractive', '-File', resolve('scripts', script), ...args], {
    encoding: 'utf8', windowsHide: true, env: { ...process.env, ...env },
  }));
}

test('cloud inventory is redacted and cannot establish account entitlement', () => {
  const sentinel = 'test-only-private-config-location-do-not-print';
  const report = run('check-prerequisites.ps1', ['-Scope', 'Cloud'], { OCI_CLI_CONFIG_FILE: sentinel });
  assert.equal(report.gateCompleted, false);
  assert.equal(report.cloud.accountEntitlementVerified, false);
  assert.equal(report.cloud.customConfigConfigured, true);
  assert.equal(report.cloud.customConfigPresent, false);
  assert.equal(report.device, undefined);
  assert.equal(JSON.stringify(report).includes(sentinel), false);
});

test('installed tools and browsers do not certify a physical phone', () => {
  const report = run('check-prerequisites.ps1', ['-Scope', 'Device']);
  assert.equal(report.gateCompleted, false);
  assert.equal(report.device.physicalPhoneBuildVerified, false);
  assert.equal(report.device.batteryAndAccessoryChecksVerified, false);
  assert.equal(report.cloud, undefined);
  assert.equal(typeof report.device.adbOnPath, 'boolean');
});

test('missing production origin leaves the HTTPS/Google gate blocked', () => {
  const report = run('check-public-origin.ps1');
  assert.equal(report.code, 'originNotConfigured');
  assert.equal(report.gateCompleted, false);
  assert.equal(report.trustedHttpsLiveness, false);
  assert.equal(report.googleOriginVerified, false);
});

for (const origin of ['http://127.0.0.1:5173', 'https://127.0.0.1', 'https://user:test-only-secret@example.com', 'https://example.com/?token=test-only-secret', 'https://example.com/callback', 'https://music.localhost', 'https://music.internal']) {
  test('rejects unsafe or non-origin input before sending a request: ' + origin.split('?')[0].replace('user:test-only-secret@', ''), () => {
    const report = run('check-public-origin.ps1', ['-Origin', origin]);
    assert.equal(report.code, 'httpsHostnameOriginRequired');
    assert.equal(report.trustedHttpsLiveness, false);
    assert.equal(report.gateCompleted, false);
    assert.equal(JSON.stringify(report).includes('test-only-secret'), false);
  });
}
