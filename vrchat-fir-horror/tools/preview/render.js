const { chromium } = require('playwright');
(async () => {
  const out = process.argv[2], fps = 30, dur = 4.6;
  const browser = await chromium.launch({ args: ['--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader'] });
  const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
  page.on('console', m => console.log('page:', m.text()));
  page.on('pageerror', e => console.log('err:', e.message));
  await page.goto('http://127.0.0.1:8765/index.html');
  await page.waitForFunction(() => window.ready !== undefined);
  await page.evaluate(() => window.ready);
  for (let i = 0; i < Math.round(fps * dur); i++) {
    await page.evaluate(t => window.renderAt(t), i / fps);
    await page.locator('canvas').screenshot({ path: `${out}/f${String(i).padStart(4, '0')}.png` });
  }
  await browser.close();
})();
