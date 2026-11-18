// Renders flythrough.html frame by frame (Playwright + the pre-installed Chromium). Each frame is read
// back from the page's own canvas, so frames never tear. Usage: node render_flythrough.js <outDir> <port>
const { chromium } = require('playwright');
const fs = require('fs');
(async () => {
  const outDir = process.argv[2], port = process.argv[3] || '8777';
  const browser = await chromium.launch({ args: ['--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader'] });
  const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
  page.on('pageerror', e => console.log('page error:', e.message));
  await page.goto(`http://127.0.0.1:${port}/flythrough.html`);
  await page.waitForFunction(() => window.ready === true, null, { timeout: 120000 });
  const n = await page.evaluate(() => window.frameCount);
  fs.mkdirSync(outDir, { recursive: true });
  for (let i = 0; i < n; i++) {
    const url = await page.evaluate(k => window.renderFrame(k), i);
    fs.writeFileSync(`${outDir}/f${String(i).padStart(5, '0')}.png`, Buffer.from(url.split(',')[1], 'base64'));
    if (i % 300 === 0) console.log('frame', i, '/', n);
  }
  fs.writeFileSync(`${outDir}/sounds.json`, JSON.stringify(await page.evaluate(() => window.sounds)));
  await browser.close();
})();
