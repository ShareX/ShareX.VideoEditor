import { spawnSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import {
  existsSync,
  mkdirSync,
  readFileSync,
  statSync,
  writeFileSync
} from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const scriptDir = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(scriptDir, '..');
const frontendDir = join(repoRoot, 'frontend');
const packageJsonPath = join(frontendDir, 'package.json');
const packageLockPath = join(frontendDir, 'package-lock.json');
const nodeModulesDir = join(frontendDir, 'node_modules');
const installedLockPath = join(nodeModulesDir, '.package-lock.json');
const stampPath = join(nodeModulesDir, '.xerahs-webui-deps.sha256');
const npmExecutable = process.argv[2] || (process.platform === 'win32' ? 'npm.cmd' : 'npm');
const maxAttempts = Math.max(1, Number.parseInt(process.env.XERAHS_NPM_CI_RETRIES || '3', 10));

function hashManifests() {
  const hash = createHash('sha256');
  hash.update('xerahs-webui-deps-v1\n');
  hash.update(`${process.platform}/${process.arch}\n`);

  for (const filePath of [packageJsonPath, packageLockPath]) {
    hash.update(`${filePath}\n`);
    hash.update(readFileSync(filePath));
    hash.update('\n');
  }

  return hash.digest('hex');
}

function latestManifestTimeMs() {
  return Math.max(
    statSync(packageJsonPath).mtimeMs,
    statSync(packageLockPath).mtimeMs
  );
}

function writeStamp(hash) {
  mkdirSync(nodeModulesDir, { recursive: true });
  writeFileSync(stampPath, `${hash}\n`, 'utf8');
}

function dependenciesAreCurrent(expectedHash) {
  if (!existsSync(installedLockPath)) {
    return false;
  }

  if (existsSync(stampPath)) {
    return readFileSync(stampPath, 'utf8').trim() === expectedHash;
  }

  if (statSync(installedLockPath).mtimeMs >= latestManifestTimeMs()) {
    console.log('[VideoEditor] Adopting existing WebUI node_modules dependency state.');
    writeStamp(expectedHash);
    return true;
  }

  return false;
}

function runNpmCi() {
  const result = spawnSync(
    npmExecutable,
    ['ci', '--no-audit', '--fund=false'],
    {
      cwd: frontendDir,
      env: process.env,
      shell: process.platform === 'win32',
      stdio: 'inherit'
    }
  );

  return result.status ?? 1;
}

function sleep(ms) {
  return new Promise(resolve => setTimeout(resolve, ms));
}

const expectedHash = hashManifests();

if (dependenciesAreCurrent(expectedHash)) {
  console.log('[VideoEditor] WebUI dependencies are current; skipping npm ci.');
  process.exit(0);
}

console.log('[VideoEditor] Restoring WebUI dependencies with npm ci...');

let exitCode = 1;
for (let attempt = 1; attempt <= maxAttempts; attempt++) {
  exitCode = runNpmCi();

  if (exitCode === 0) {
    writeStamp(expectedHash);
    process.exit(0);
  }

  if (attempt < maxAttempts) {
    const delayMs = attempt * 1500;
    console.warn(`[VideoEditor] npm ci failed with exit code ${exitCode}; retrying in ${delayMs}ms (${attempt}/${maxAttempts}).`);
    await sleep(delayMs);
  }
}

console.error(`[VideoEditor] npm ci failed after ${maxAttempts} attempt(s).`);
process.exit(exitCode);
