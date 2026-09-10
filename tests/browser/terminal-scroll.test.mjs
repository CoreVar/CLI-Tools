import assert from 'node:assert/strict';
import { after, before, test } from 'node:test';
import { createServer } from 'node:http';
import { readFile } from 'node:fs/promises';
import { chromium } from 'playwright';

let server, browser, origin;
before(async () => {
  server = createServer(async (request, response) => {
    const name = request.url.slice(1);
    if (['terminal.js', 'terminal-scroll.js'].includes(name)) {
      response.setHeader('Content-Type', 'text/javascript');
      response.end(await readFile(new URL(`../../src/CommandLineInterface.Blazor/wwwroot/${name}`, import.meta.url)));
    } else {
      response.setHeader('Content-Type', 'text/html');
      response.end('<!doctype html><html><body></body></html>');
    }
  });
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  origin = `http://127.0.0.1:${server.address().port}`;
  browser = await chromium.launch({ headless: true, ...(process.env.BROWSER_CHANNEL ? { channel: process.env.BROWSER_CHANNEL } : {}) });
});
after(async () => {
  await browser?.close();
  await new Promise(resolve => server?.close(resolve));
});

const settle = page => page.evaluate(() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(() => requestAnimationFrame(resolve)))));
const append = async page => {
  await page.evaluate(() => {
    for (let i = 0; i < 8; i++) {
      const line = document.createElement('div');
      line.textContent = `Streamed result ${document.querySelector('#output').childElementCount}`;
      document.querySelector('#output').append(line);
    }
  });
  await settle(page);
};
const position = page => page.evaluate(() => ({ top: window.scroller.scrollTop, bottom: window.scroller.scrollHeight - window.scroller.clientHeight }));
const assertBottom = async page => {
  const { top, bottom } = await position(page);
  assert.ok(Math.abs(top - bottom) < 4, `Expected bottom ${bottom}, got ${top}`);
};

for (const nested of [false, true]) for (const width of [390, 1280]) {
  test(`${nested ? 'portal wrapper' : 'standalone'} at ${width}px follows streaming and respects reader intent`, async () => {
    const page = await browser.newPage({ viewport: { width, height: 800 } });
    try {
      await page.goto(origin);
      await page.setContent(`<style>
        body{margin:0;height:1200px} #wrapper{width:100%;box-sizing:border-box}
        #root{font:14px/20px monospace;box-sizing:border-box;overflow-anchor:none}
        #output>div{height:20px} input{height:22px}
        ${nested ? '#wrapper{height:240px;overflow:auto}#root{overflow:visible;min-height:100%}' : '#root{height:240px;overflow:auto}'}
      </style><div id="wrapper"><div id="root"><div id="output"></div><input class="terminal-input"></div></div><button id="elsewhere">Elsewhere</button>`);
      await page.evaluate(async nested => {
        window.scroller = document.querySelector(nested ? '#wrapper' : '#root');
        const { initializeTerminal } = await import('/terminal.js');
        window.handle = initializeTerminal(document.querySelector('#root'), document.querySelector('input'), {
          invokeMethodAsync(name, key) {
            if (name === 'HandleTerminalKey' && key === 'Enter') return new Promise(resolve => { window.finishCommand = resolve; });
            return Promise.resolve();
          }
        }, false);
      }, nested);
      await page.locator('input').focus();
      await page.locator('input').press('Enter');
      for (let i = 0; i < 6; i++) { await append(page); await assertBottom(page); }
      assert.equal(await page.evaluate(() => window.scrollY), 0, 'Must not scroll the enclosing page');

      // Wheel upward while more output arrives: the queued follow frame must yield immediately.
      await page.mouse.move(100, 100);
      await page.mouse.wheel(0, -180);
      await settle(page);
      const paused = (await position(page)).top;
      await append(page);
      assert.equal((await position(page)).top, paused);

      // Returning manually to the bottom resumes following.
      await page.evaluate(() => { window.scroller.scrollTop = window.scroller.scrollHeight; });
      await settle(page);
      await append(page); await assertBottom(page);

      // Focus can move away before an asynchronous command completes; completion must not steal it.
      await page.locator('#elsewhere').click();
      const unfocused = (await position(page)).top;
      await append(page);
      await page.evaluate(() => window.finishCommand());
      await settle(page);
      assert.equal((await position(page)).top, unfocused);
      assert.equal(await page.evaluate(() => document.activeElement.id), 'elsewhere');

      // A new submission resumes from an older scroll position before the command finishes.
      await page.locator('input').focus();
      await page.evaluate(() => { window.scroller.scrollTop = 0; });
      await settle(page);
      await page.locator('input').press('Enter');
      await settle(page); await assertBottom(page);
      await append(page); await assertBottom(page);

      // Selection pauses output following so copied text stays under the pointer.
      await page.evaluate(() => {
        const range = document.createRange(); range.selectNodeContents(document.querySelector('#output').lastChild);
        const selection = window.getSelection(); selection.removeAllRanges(); selection.addRange(range);
      });
      await settle(page);
      const selected = (await position(page)).top;
      await append(page);
      assert.equal((await position(page)).top, selected);
      assert.ok(await page.evaluate(() => window.getSelection().toString().startsWith('Streamed result')));

      await page.evaluate(() => { window.getSelection().removeAllRanges(); window.finishCommand(); });
      await settle(page);
      await page.locator('input').press('Enter');
      await settle(page); await assertBottom(page);
      await page.evaluate(() => { window.scroller.style.height = '160px'; });
      await settle(page); await assertBottom(page);

      await page.evaluate(() => { window.handle.dispose(); window.finishCommand(); });
      const disposed = (await position(page)).top;
      await append(page);
      assert.equal((await position(page)).top, disposed, 'Disposed terminal must stop following');
    } finally { await page.close(); }
  });
}
