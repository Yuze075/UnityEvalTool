# UnityEvalTool Broker

This directory contains the .NET NativeAOT Broker/CLI and npm release pipeline for
UnityEvalTool. The Unity Package Manager package lives separately under
`Packages/com.yuzetoolkit.unityevaltool`.

The installed `unity` executable owns all runtime behavior: the localhost Broker,
MCP endpoint, Unity registration, CLI routing, and current-user service management.
The npm JavaScript files only select and launch the native package for the current
platform.

Supported release RIDs:

- `osx-arm64`, `osx-x64`
- `win-arm64`, `win-x64`
- `linux-arm64`, `linux-x64`

Build the current platform package with:

```bash
node npm/scripts/pack-platform.mjs
```

Build the platform-independent entry package with:

```bash
node npm/scripts/pack-root.mjs
```

NativeAOT platform packages are built on matching GitHub Actions runners. Publishing
is a separate, explicit step and must not be performed merely to validate packaging.

## Package set

Every version is released as one entry package and six optional native packages:

- `@yuzetoolkit/unityevaltool`
- `@yuzetoolkit/unityevaltool-darwin-arm64`
- `@yuzetoolkit/unityevaltool-darwin-x64`
- `@yuzetoolkit/unityevaltool-linux-arm64`
- `@yuzetoolkit/unityevaltool-linux-x64`
- `@yuzetoolkit/unityevaltool-win32-arm64`
- `@yuzetoolkit/unityevaltool-win32-x64`

Publish all six native packages before the entry package. This prevents users from
installing an entry version whose matching native dependency is not available yet.

## Release checklist

1. Keep the Unity package and npm package versions identical.
2. Build `Broker/UnityEvalTool.Broker.slnx` and run its tests in Release configuration.
3. Run `release.yml` with `publish=false` and verify all seven artifacts.
4. Install the entry tarball together with the current platform tarball and run
   `unity --help` and `unity doctor`.
5. Run the workflow with `publish=true` only when the `NPM_TOKEN` repository secret is
   configured, or download the verified artifacts and publish them locally.
6. Publish native packages first, entry package last, then create the matching
   `v<version>` GitHub release and attach all seven tarballs.

Do not reuse a published npm version. If any package in the set was published, advance
the version for the whole set before rebuilding.
