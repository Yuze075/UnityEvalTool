'use strict';

const { spawnSync } = require('node:child_process');
const { resolveNativeExecutable } = require('./native-package');

try {
  spawnSync(resolveNativeExecutable(), ['service', 'uninstall'], {
    stdio: 'inherit',
    windowsHide: true
  });
} catch (error) {
  console.warn(`UnityEvalTool service removal was skipped: ${error.message}`);
}
