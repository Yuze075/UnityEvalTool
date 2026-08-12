'use strict';

const { spawnSync } = require('node:child_process');
const { resolveNativeExecutable } = require('./native-package');

try {
  const result = spawnSync(resolveNativeExecutable(), ['service', 'install'], {
    stdio: 'inherit',
    windowsHide: true
  });
  if (result.error || result.status !== 0) {
    console.warn(
      'UnityEvalTool was installed, but its current-user service could not be installed. ' +
      'Run `unity service install` after resolving the reported error.'
    );
  }
} catch (error) {
  console.warn(`UnityEvalTool service installation was skipped: ${error.message}`);
}
