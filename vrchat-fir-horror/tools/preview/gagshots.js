const { chromium } = require('playwright');
(async () => {
  const shots = [
    ['salute', 0, ['white','white','white'], 2.0, 4.5, 0], ['shimmy', 1, ['white'], 1.3, 4, 30], ['dance', 2, ['white'], 1.0, 5.5, 0],
    ['shrug', 2, ['white'], 3.5, 5.5, 0], ['collapse', 3, ['white'], 2.5, 5, 35], ['lazy', 4, ['red'], 2.0, 4, 20],
    ['mic', 5, ['white'], 2.0, 4, 30], ['point', 6, ['red'], 1.5, 6, 30], ['laugh', 6, ['red'], 3.5, 6, 30],
    ['phone', 7, ['white'], 2.0, 4, 25], ['armfall', 8, ['white'], 2.0, 4.5, 30], ['hips', 9, ['green'], 2.0, 4, 0],
    ['tremble', 10, ['white'], 2.0, 4, 20], ['bow', 11, ['white','red'], 2.5, 4.5, 60],
  ];
  const b = await chromium.launch({ args: ['--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader'] });
  const p = await b.newPage({ viewport: { width: 640, height: 640 } });
  p.on('pageerror', e => console.log('err:', e.message));
  await p.goto('http://127.0.0.1:8765/skelgags.html');
  await p.waitForFunction(() => window.ready !== undefined, null, { timeout: 60000 }); await p.evaluate(() => window.ready);
  for (const [name, act, cols, t, L, cy] of shots) {
    await p.evaluate(([a, c, t, L, cy]) => window.show(a, c, t, L, cy), [act, cols, t, L, cy]);
    await p.screenshot({ path: `sg_${name}.png` });
  }
  await b.close();
})();
