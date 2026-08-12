import { execFileSync } from 'node:child_process';
import { cpSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { platform } from 'node:process';
import { fileURLToPath } from 'node:url';

const scriptDirectory = dirname(fileURLToPath(import.meta.url));
const root = resolve(scriptDirectory, '../..');
const source = join(root, 'npm/root');
const stage = join(root, 'artifacts/npm/root');
const version = process.env.UNITY_EVAL_TOOL_VERSION ?? '2.0.0';
rmSync(stage, { recursive: true, force: true });
mkdirSync(stage, { recursive: true });
cpSync(source, stage, { recursive: true });
cpSync(join(root, '../LICENSE'), join(stage, 'LICENSE'));

const manifestPath = join(stage, 'package.json');
const manifest = JSON.parse(readFileSync(manifestPath, 'utf8'));
manifest.version = version;
for (const dependency of Object.keys(manifest.optionalDependencies)) {
  manifest.optionalDependencies[dependency] = version;
}
writeFileSync(manifestPath, JSON.stringify(manifest, null, 2) + '\n');
const npmExecutable = platform === 'win32' ? 'npm.cmd' : 'npm';
execFileSync(npmExecutable, ['pack', stage, '--ignore-scripts', '--pack-destination', join(root, 'artifacts/npm')], { stdio: 'inherit' });
