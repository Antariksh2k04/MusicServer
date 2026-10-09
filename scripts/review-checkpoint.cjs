const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');

const root = path.resolve(__dirname, '..');
const review = path.join(root, '.local/review');
const pointer = path.join(review, 'last-review.json');
const extensions = new Set(['.md', '.cs', '.csproj', '.props', '.targets', '.sln', '.slnx',
  '.ts', '.tsx', '.js', '.jsx', '.cjs', '.mjs', '.json', '.css', '.html', '.ps1', '.yaml', '.yml',
  '.kt', '.java', '.xml', '.gradle', '.kts', '.properties', '.sql', '.cmd']);
const excludedDirectories = new Set(['node_modules', 'bin', 'obj', 'dist', '.git', '.local',
  '.tools', '.gradle', 'build', 'TestResults', 'playwright-report']);
const files = {};
const skipped = [];
function visit(relative) {
  const absolute = path.join(root, relative);
  const stat = fs.lstatSync(absolute);
  if (stat.isSymbolicLink()) { skipped.push(relative); return; }
  if (stat.isDirectory()) {
    if (excludedDirectories.has(path.basename(relative))) return;
    for (const name of fs.readdirSync(absolute).sort()) visit(path.join(relative, name));
    return;
  }
  const name = path.basename(relative);
  if (/^\.env(?:\.|$)/i.test(name) || /\.local\./i.test(name)) return;
  if (!extensions.has(path.extname(name).toLowerCase()) && name !== '.gitignore') {
    skipped.push(relative); return;
  }
  const bytes = fs.readFileSync(absolute);
  files[relative.replaceAll('\\', '/')] = crypto.createHash('sha256').update(bytes).digest('hex');
}
for (const name of fs.readdirSync(root).sort()) {
  if (fs.lstatSync(path.join(root, name)).isFile()) visit(name);
}
for (const dir of ['src', 'tests', 'scripts', 'openspec']) {
  if (fs.existsSync(path.join(root, dir))) visit(dir);
}
const command = process.argv[2];
if (command === 'compare') {
  if (!fs.existsSync(pointer)) {
    console.log(JSON.stringify({ baselineAvailable: false, files: Object.keys(files), skipped }, null, 2));
  } else {
    const previous = JSON.parse(fs.readFileSync(pointer, 'utf8'));
    const changes = Object.keys({ ...previous.files, ...files }).sort().flatMap(file =>
      previous.files[file] === files[file] ? [] : [{ file,
        kind: !previous.files[file] ? 'added' : !files[file] ? 'deleted' : 'modified' }]);
    console.log(JSON.stringify({ baselineAvailable: true, checkpoint: previous.id, changes, skipped }, null, 2));
  }
} else if (command === 'capture') {
  const id = new Date().toISOString().replaceAll(':', '-') + '-' + crypto.randomUUID();
  const destination = path.join(review, 'checkpoints', id, 'files');
  for (const [file, hash] of Object.entries(files)) {
    const bytes = fs.readFileSync(path.join(root, file));
    if (crypto.createHash('sha256').update(bytes).digest('hex') !== hash) {
      throw new Error('Workspace changed during capture; checkpoint was not advanced.');
    }
    const target = path.join(destination, file);
    fs.mkdirSync(path.dirname(target), { recursive: true });
    fs.writeFileSync(target, bytes, { flag: 'wx' });
  }
  const manifest = JSON.stringify({ id, createdAtUtc: new Date().toISOString(), files, skipped }, null, 2);
  fs.writeFileSync(path.join(review, 'checkpoints', id, 'manifest.json'), manifest);
  const temporary = pointer + '.' + crypto.randomUUID() + '.tmp';
  fs.writeFileSync(temporary, manifest, { flag: 'wx' });
  fs.renameSync(temporary, pointer);
  console.log(JSON.stringify({ checkpoint: id, fileCount: Object.keys(files).length, skipped }, null, 2));
} else {
  throw new Error('Use compare or capture. Capture only after completing a review.');
}
