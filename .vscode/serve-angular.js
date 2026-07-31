// Starts an Nx/Angular dev server, unless one is already serving on that port.
//
// Usage: node .vscode/serve-angular.js <project> <port> [configuration]
//   e.g. node .vscode/serve-angular.js web 4200 development
//        node .vscode/serve-angular.js docs 4300 staging
//
// Why this wrapper exists: when the port is taken, @angular/build asks
// "Would you like to use a different port?" and waits for an answer. The guard
// meant to skip that prompt is `if (!tty_1.isTTY)` in
// @angular/build/src/utils/check-port.js — isTTY is a *function*, never called,
// so the check is always false and the prompt always appears. NG_FORCE_TTY and
// CI therefore have no effect. In a debug session the prompt hangs the
// preLaunchTask forever and the browser never launches.

const net = require('node:net');
const path = require('node:path');
const { spawn } = require('node:child_process');

const [project, portArg, configuration = 'development'] = process.argv.slice(2);

if (!project || !portArg) {
  console.error('Usage: node serve-angular.js <project> <port> [configuration]');
  process.exit(1);
}

const PORT = Number(portArg);
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
    // Note: this reuses whatever is already on the port. If you switched
    // configuration (development <-> staging) you must stop the old server
    // first, otherwise you attach to a bundle built the previous way.
    console.log(`Reusing the dev server already listening on port ${PORT}.`);
    console.log(`  ->  Local:   http://localhost:${PORT}/`);
    return;
  }

  // Single command string: passing an args array alongside shell:true is
  // deprecated (DEP0190) because the arguments are concatenated unescaped.
  const child = spawn(`npx nx serve ${project} --configuration ${configuration}`, {
    cwd: FRONTEND,
    stdio: 'inherit',
    shell: true,
  });
  child.on('exit', (code) => process.exit(code ?? 0));
});
