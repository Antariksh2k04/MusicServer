// Temporary runner bootstrap. Incorporate its reviewed sources/locks before native development.
const { spawnSync } = require('node:child_process');
const { cpSync, existsSync, mkdirSync, readFileSync, writeFileSync } = require('node:fs');
const { resolve } = require('node:path');

function extendManifest(manifest, pins) {
  return {
    ...manifest,
    dependencies: { ...manifest.dependencies, '@capacitor/core': pins.capacitor, '@capacitor/android': pins.capacitor },
    devDependencies: { ...manifest.devDependencies, '@capacitor/cli': pins.capacitor },
  };
}

function assertCapacitorLock(lock, pins) {
  for (const name of ['core', 'android', 'cli']) {
    const item = lock.packages?.[`node_modules/@capacitor/${name}`];
    if (item?.version !== pins.capacitor || !item.integrity?.startsWith('sha512-')) {
      throw new Error(`Missing verified lock entry for @capacitor/${name}`);
    }
  }
}

function assertTemplate({ variables, build, wrapper }, pins) {
  for (const [name, value] of Object.entries({ minSdkVersion: pins.minSdk, compileSdkVersion: pins.compileSdk, targetSdkVersion: pins.targetSdk })) {
    if (!new RegExp(`\\b${name}\\s*=\\s*${value}\\b`).test(variables)) throw new Error(`Unexpected template ${name}`);
  }
  if (!build.includes(`com.android.tools.build:gradle:${pins.androidGradlePlugin}'`)) throw new Error('Unexpected Android Gradle Plugin');
  if (!wrapper.includes(`gradle-${pins.gradle}-all.zip`)) throw new Error('Unexpected Gradle wrapper version');
}

function run(command, args, cwd) {
  const result = spawnSync(command, args, { cwd, stdio: 'inherit', shell: false });
  if (result.error) throw result.error;
  if (result.status !== 0) throw new Error(`${command} failed (${result.status ?? result.signal})`);
}

function main() {
  if (process.platform !== 'linux' || process.env.GITHUB_ACTIONS !== 'true') {
    throw new Error('Use the manual GitHub Actions bootstrap; this script requires its Linux runner');
  }
  const root = resolve(__dirname, '..');
  const pins = JSON.parse(readFileSync(resolve(root, 'scripts/android-toolchain.json'), 'utf8'));
  const web = resolve(root, 'MusicServerFrontend');
  const stage = resolve(root, '.local/android-bootstrap');
  // Refuse reuse rather than deleting or overwriting a previous bootstrap.
  if (existsSync(stage)) throw new Error('Bootstrap directory already exists; use a fresh checkout');
  mkdirSync(stage, { recursive: true });
  for (const name of ['package.json', 'package-lock.json', 'tsconfig.json', 'vite.config.ts', 'vite.native.config.ts', 'index.html', 'capacitor.config.json', 'native-shell', 'src']) {
    cpSync(resolve(web, name), resolve(stage, name), { recursive: true });
  }
  const manifest = extendManifest(JSON.parse(readFileSync(resolve(stage, 'package.json'), 'utf8')), pins);
  writeFileSync(resolve(stage, 'package.json'), JSON.stringify(manifest, null, 2) + '\n');
  run('npm', ['install', '--package-lock-only', '--no-audit', '--no-fund'], stage);
  assertCapacitorLock(JSON.parse(readFileSync(resolve(stage, 'package-lock.json'), 'utf8')), pins);
  run('npm', ['ci', '--no-audit', '--no-fund'], stage);
  run('npm', ['test'], stage);
  run('npm', ['run', 'build'], stage);
  run('npm', ['run', 'build:native-shell'], stage);
  const cli = resolve(stage, 'node_modules/@capacitor/cli/bin/capacitor');
  run(process.execPath, [cli, 'add', 'android'], stage);
  const android = resolve(stage, 'android');
  assertTemplate({
    variables: readFileSync(resolve(android, 'variables.gradle'), 'utf8'),
    build: readFileSync(resolve(android, 'build.gradle'), 'utf8'),
    wrapper: readFileSync(resolve(android, 'gradle/wrapper/gradle-wrapper.properties'), 'utf8'),
  }, pins);
  const appBuild = resolve(android, 'app/build.gradle');
  const original = readFileSync(appBuild, 'utf8');
  if ((original.match(/^android \{/gm) ?? []).length !== 1) throw new Error('Unexpected application Gradle template');
  writeFileSync(appBuild, original.replace(/^android \{/m, `android {\n    buildToolsVersion "${pins.buildTools}"`));
  run(process.execPath, [cli, 'sync', 'android'], stage);
  writeFileSync(resolve(stage, 'bootstrap-report.json'), JSON.stringify({
    commit: process.env.GITHUB_SHA,
    runId: process.env.GITHUB_RUN_ID,
    node: process.version,
    selectedPins: pins,
    generatedTemplateVerified: true,
    apkBuildVerified: false,
    physicalDeviceVerified: false,
    backgroundPlaybackVerified: false,
  }, null, 2) + '\n');
}

module.exports = { extendManifest, assertCapacitorLock, assertTemplate };
if (require.main === module) main();
