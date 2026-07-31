// Starts the Angular dev server for apps/web, unless one is already serving.
//
// Why this wrapper exists: when port 4200 is taken, @angular/build asks
// "Would you like to use a different port?" and waits for an answer. The guard
// meant to skip that prompt is `if (!tty_1.isTTY)` in
// @angular/build/src/utils/check-port.js — isTTY is a *function*, never called,
// so the check is always false and the prompt always appears. NG_FORCE_TTY and
// CI therefore have no effect. In a debug session the prompt hangs the
// preLaunchTask forever and the browser never launches.

const net = require('node:net');
const path = require('node:path');
const { spawn } = require('node:child_process');

const PORT = 4200;
const FRONTEND = path.join(__dirname, '..', 'frontend');

// Both stacks: Node binds localhost to ::1 first on Windows, so an existing
// dev server is frequently reachable on ::1 and not on 127.0.0.1.
const HOSTS = ['::1', '127.0.0.1'];

function isHostServing(host) {
  return new Promise((resolve) => {
    const socket = net.connect({ port: PORT, host });
    const done = (result) => {
      socket.destroy();
      resolve(result);
    };
    socket.once('connect', () => done(true));
    socket.once('error', () => done(false));
    socket.setTimeout(1000, () => done(false));
  });
}

async function isPortServing() {
  const results = await Promise.all(HOSTS.map(isHostServing));
  return results.some(Boolean);
}

isPortServing().then((serving) => {
  if (serving) {
    console.log(`Reusing the dev server already listening on port ${PORT}.`);
    console.log(`  ->  Local:   http://localhost:${PORT}/`);
    return;
  }

  // Single command string: passing an args array alongside shell:true is
  // deprecated (DEP0190) because the arguments are concatenated unescaped.
  const child = spawn('npx nx serve web', {
    cwd: FRONTEND,
    stdio: 'inherit',
    shell: true,
  });
  child.on('exit', (code) => process.exit(code ?? 0));
});
