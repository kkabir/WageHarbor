// Renders an HTML poster to PNG at its exact size, after web fonts have loaded.
// Usage (from the repo root):
//   node tools/browser/render-poster.mjs marketing/poster.html marketing/WageHarbor-poster.png 1080 1350
import { launch } from './cdp.mjs';
import { rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { pathToFileURL } from 'node:url';

const [html, png, w = '1080', h = '1350'] = process.argv.slice(2);
if (!html || !png) { console.error('Usage: node render-poster.mjs <poster.html> <output.png> [width] [height]'); process.exit(1); }
const width = Number(w), height = Number(h);
const profile = join(tmpdir(), 'wageharbor-poster-chrome');
rmSync(profile, { recursive: true, force: true });

const b = await launch({ profileDir: profile, width, height });
await b.goto(pathToFileURL(resolve(html)).href, 500);
await b.eval('document.fonts.ready.then(() => true)');
await new Promise(r => setTimeout(r, 800));
if (!await b.eval(`document.fonts.check('900 40px Outfit')`)) console.warn('Warning: the Outfit web font did not load (no internet?).');
await b.shot(resolve(png), { x: 0, y: 0, width, height });
await b.close();
console.log('Saved', png);
