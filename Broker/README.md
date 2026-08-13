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

1. Keep `version.json`, UnityEvalTool, Broker and npm versions identical. UnityDebugTool
   has its own SemVer in `debugPackageVersion`, but its UnityEvalTool dependency must match.
2. Build `Broker/UnityEvalTool.Broker.slnx` and run its tests in Release configuration.
3. Run `release.yml` with `publish=false`; the workflow verifies all seven artifacts,
   starts every packed native executable, and installs the Linux entry/native tarball pair
   before running `unity --help` and `unity doctor`.
4. Inspect the retained artifacts when additional platform-specific verification is needed.
5. Configure every npm package's Trusted Publisher for repository
   `Yuze075/UnityEvalTool`, workflow `release.yml`, and `npm publish` permission. The
   workflow uses GitHub OIDC and does not require an `NPM_TOKEN` secret.
6. Run the workflow with `publish=true`; only the publish job receives `id-token: write`,
   and npm verifies the repository/workflow identity before accepting an artifact.
7. Publish native packages first, entry package last, then create the matching
   `v<version>` GitHub release and attach all seven tarballs.

Do not replace a published npm version. A failed publish job may be retried with the exact
retained artifacts: preflight verifies each existing npm tarball by SHA-1 and publishes only
the missing packages. If the artifacts are rebuilt or their bytes differ after any package in
the set was published, advance the version for the whole set.
