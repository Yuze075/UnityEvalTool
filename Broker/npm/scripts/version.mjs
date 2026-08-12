import { readFileSync } from 'node:fs';
import { join } from 'node:path';

export function resolveAndValidateVersion(brokerRoot) {
  const repositoryRoot = join(brokerRoot, '..');
  const metadata = JSON.parse(readFileSync(join(repositoryRoot, 'version.json'), 'utf8'));
  const version = metadata.version;
  if (!/^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$/.test(version)) {
    throw new Error(`version.json contains invalid SemVer '${version}'.`);
  }
  if (process.env.UNITY_EVAL_TOOL_VERSION && process.env.UNITY_EVAL_TOOL_VERSION !== version) {
    throw new Error(`Requested version ${process.env.UNITY_EVAL_TOOL_VERSION} does not match version.json ${version}.`);
  }

  const unityManifest = JSON.parse(readFileSync(
    join(repositoryRoot, 'Packages/com.yuzetoolkit.unityevaltool/package.json'), 'utf8'));
  const npmManifest = JSON.parse(readFileSync(join(brokerRoot, 'npm/root/package.json'), 'utf8'));
  const directoryProps = readFileSync(join(brokerRoot, 'Directory.Build.props'), 'utf8');
  const unityVersionSource = readFileSync(join(repositoryRoot,
    'Packages/com.yuzetoolkit.unityevaltool/Runtime/Core/UnityEvalToolVersion.cs'), 'utf8');

  const mismatches = [];
  if (unityManifest.version !== version) mismatches.push(`Unity package=${unityManifest.version}`);
  if (npmManifest.version !== version) mismatches.push(`npm entry=${npmManifest.version}`);
  for (const [name, dependencyVersion] of Object.entries(npmManifest.optionalDependencies ?? {})) {
    if (dependencyVersion !== version) mismatches.push(`${name}=${dependencyVersion}`);
  }
  if (!directoryProps.includes(`<Version>${version}</Version>`)) mismatches.push('Broker Directory.Build.props');
  if (!unityVersionSource.includes(`Current = "${version}"`)) mismatches.push('UnityEvalToolVersion.Current');
  if (mismatches.length) {
    throw new Error(`UnityEvalTool version mismatch: ${mismatches.join(', ')}; expected ${version}.`);
  }
  return version;
}
