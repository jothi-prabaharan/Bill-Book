// Sends ESC/POS bytes to a receipt printer (TK-41). Node only: main.js calls
// it for the till's `pos:print` request. No native module is needed.
//
//   { kind: 'device', path }        a path the OS exposes: /dev/usb/lp0,
//                                    /dev/ttyUSB0, \\.\COM3, \\localhost\Receipt
//   { kind: 'network', host, port } raw TCP, port 9100 by default
const fs = require('fs');
const net = require('net');

const NETWORK_TIMEOUT_MS = 5000;

function printRaw(buffer, target) {
  if (!target || typeof target !== 'object') {
    return Promise.reject(new Error('No receipt printer is set up on this till.'));
  }

  if (target.kind === 'device') {
    if (typeof target.path !== 'string' || target.path.trim() === '') {
      return Promise.reject(new Error('The receipt printer has no device path.'));
    }
    // A printer device is written, never created: 'r+' fails on a missing
    // path instead of leaving a file named like a printer on the disk.
    return new Promise((resolve, reject) => {
      fs.open(target.path, 'r+', (openError, fd) => {
        if (openError) {
          reject(new Error(`The receipt printer at ${target.path} could not be opened: ${openError.message}`));
          return;
        }
        fs.write(fd, buffer, 0, buffer.length, null, (writeError) => {
          fs.close(fd, () => {
            if (writeError) {
              reject(new Error(`The receipt printer at ${target.path} refused the receipt: ${writeError.message}`));
            } else {
              resolve();
            }
          });
        });
      });
    });
  }

  if (target.kind === 'network') {
    const port = Number.isInteger(target.port) && target.port > 0 ? target.port : 9100;
    return new Promise((resolve, reject) => {
      const socket = net.createConnection({ host: target.host, port });
      socket.setTimeout(NETWORK_TIMEOUT_MS);
      socket.on('connect', () => socket.end(buffer));
      socket.on('close', (hadError) => {
        if (!hadError) resolve();
      });
      socket.on('timeout', () => {
        socket.destroy();
        reject(new Error(`The receipt printer at ${target.host}:${port} did not answer.`));
      });
      socket.on('error', (error) => {
        reject(new Error(`The receipt printer at ${target.host}:${port} could not be reached: ${error.message}`));
      });
    });
  }

  return Promise.reject(new Error(`Unknown receipt printer kind: ${target.kind}`));
}

module.exports = { printRaw };
