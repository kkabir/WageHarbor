// Minimal Chrome DevTools Protocol driver (no npm packages): headless Chrome, navigate, run JS, screenshot.
import { spawn } from 'node:child_process';
import { writeFileSync, mkdirSync } from 'node:fs';
import { dirname, join } from 'node:path';

const CHROME = 'C:/Program Files/Google/Chrome/Application/chrome.exe';
const PORT = 9333;

export async function launch({ profileDir, width = 1366, height = 860 }) {
  const proc = spawn(CHROME, [
    '--headless=new', `--remote-debugging-port=${PORT}`, `--user-data-dir=${profileDir}`,
    '--no-first-run', '--no-default-browser-check', '--hide-scrollbars', '--allow-file-access-from-files',
    `--window-size=${width},${height}`, 'about:blank'
  ], { stdio: 'ignore' });
  let targets = [];
  for (let i = 0; i < 50; i++) {
    try { targets = await (await fetch(`http://127.0.0.1:${PORT}/json/list`)).json(); if (targets.some(t => t.type === 'page')) break; } catch { }
    await sleep(200);
  }
  const page = targets.find(t => t.type === 'page');
  const ws = new WebSocket(page.webSocketDebuggerUrl);
  await new Promise(r => ws.addEventListener('open', r, { once: true }));
  let id = 0; const pending = new Map(); const waiters = [];
  ws.addEventListener('message', e => {
    const m = JSON.parse(e.data);
    if (m.id && pending.has(m.id)) { const p = pending.get(m.id); pending.delete(m.id); m.error ? p.reject(new Error(m.error.message)) : p.resolve(m.result); }
    else if (m.method) waiters.filter(w => w.method === m.method).forEach(w => { waiters.splice(waiters.indexOf(w), 1); w.resolve(m.params); });
  });
  const send = (method, params = {}) => new Promise((resolve, reject) => { const i = ++id; pending.set(i, { resolve, reject }); ws.send(JSON.stringify({ id: i, method, params })); });
  const once = method => new Promise(resolve => waiters.push({ method, resolve }));
  await send('Page.enable');
  await send('Emulation.setDeviceMetricsOverride', { width, height, deviceScaleFactor: 1, mobile: false });

  const api = {
    send,
    async viewport(w, h, scale = 1, mobile = false) { await send('Emulation.setDeviceMetricsOverride', { width: w, height: h, deviceScaleFactor: scale, mobile }); },
    async goto(url, settleMs = 1800) { const loaded = once('Page.loadEventFired'); await send('Page.navigate', { url }); await loaded; await sleep(settleMs); },
    async eval(expr) {
      const r = await send('Runtime.evaluate', { expression: expr, awaitPromise: true, returnByValue: true });
      if (r.exceptionDetails) throw new Error(r.exceptionDetails.exception?.description || r.exceptionDetails.text);
      return r.result.value;
    },
    async waitFor(expr, timeoutMs = 15000) {
      const end = Date.now() + timeoutMs;
      while (Date.now() < end) { if (await api.eval(`!!(${expr})`)) return; await sleep(250); }
      throw new Error('Timed out waiting for ' + expr);
    },
    async shot(file, clip) {
      const jpeg = /\.jpe?g$/i.test(file);
      const r = await send('Page.captureScreenshot', { format: jpeg ? 'jpeg' : 'png', ...(jpeg ? { quality: 82 } : {}), ...(clip ? { clip: { ...clip, scale: 1 } } : {}), captureBeyondViewport: !!clip });
      mkdirSync(dirname(file), { recursive: true });
      writeFileSync(file, Buffer.from(r.data, 'base64'));
    },
    // Chrome may exit before it answers Browser.close, so don't wait for the reply.
    async close() { send('Browser.close').catch(() => { }); await sleep(200); ws.close(); proc.kill(); }
  };
  return api;
}

export const sleep = ms => new Promise(r => setTimeout(r, ms));
export { join };
