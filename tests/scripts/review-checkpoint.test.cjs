const { execFileSync } = require('node:child_process');
const fs = require('node:fs');
const path = require('node:path');
const { test } = require('node:test');
const assert = require('node:assert/strict');

test('checkpoints retain exact source and compare additions, edits, and deletions', t => {
  const parent = path.resolve('.tools/review-checkpoint-tests');
  fs.mkdirSync(parent, { recursive: true });
  const root = fs.mkdtempSync(path.join(parent, 'case-'));
  t.after(() => {
    const target = path.resolve(root);
    assert.equal(path.dirname(target), parent);
    assert.ok(path.basename(target).startsWith('case-'));
    fs.rmSync(target, { recursive: true });
  });
  fs.mkdirSync(path.join(root, 'scripts'));
  fs.copyFileSync('scripts/review-checkpoint.cjs', path.join(root, 'scripts/review-checkpoint.cjs'));
  fs.writeFileSync(path.join(root, 'A.md'), 'original');
  fs.writeFileSync(path.join(root, 'C.md'), 'delete me');
  fs.writeFileSync(path.join(root, '.env'), 'do not capture');
  fs.writeFileSync(path.join(root, 'private.pem'), 'do not capture');
  fs.writeFileSync(path.join(root, 'appsettings.debug.local.json'), 'do not capture');
  function run(command) {
    return JSON.parse(execFileSync(process.execPath, [path.join(root, 'scripts/review-checkpoint.cjs'), command], { encoding: 'utf8', windowsHide: true }));
  }
  assert.equal(run('compare').baselineAvailable, false);
  const saved = run('capture');
  assert.deepEqual(run('compare').changes, []);
  const originals = path.join(root, '.local/review/checkpoints', saved.checkpoint, 'files');
  assert.equal(fs.readFileSync(path.join(originals, 'A.md'), 'utf8'), 'original');
  for (const name of ['.env', 'private.pem', 'appsettings.debug.local.json']) {
    assert.equal(fs.existsSync(path.join(originals, name)), false);
  }
  fs.writeFileSync(path.join(root, 'A.md'), 'edited');
  fs.writeFileSync(path.join(root, 'B.md'), 'added');
  fs.unlinkSync(path.join(root, 'C.md'));
  assert.deepEqual(run('compare').changes, [
    { file: 'A.md', kind: 'modified' }, { file: 'B.md', kind: 'added' }, { file: 'C.md', kind: 'deleted' },
  ]);
  run('capture');
  assert.deepEqual(run('compare').changes, []);
  assert.equal(fs.readFileSync(path.join(originals, 'A.md'), 'utf8'), 'original');
});
