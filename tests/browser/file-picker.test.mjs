import assert from 'node:assert/strict';
import { before, after, test } from 'node:test';
import { execFileSync, spawn } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { mkdir } from 'node:fs/promises';
import { chromium } from 'playwright';

const project = fileURLToPath(new URL('./FilePickerHost/FilePickerHost.csproj', import.meta.url));
let host, browser, origin, output = '';
before(async () => {
  execFileSync('dotnet', ['build', project, '--verbosity', 'quiet', '-clp:ErrorsOnly'], { timeout: 180000 });
  const assembly = fileURLToPath(new URL('./FilePickerHost/bin/Debug/net10.0/FilePickerHost.dll', import.meta.url));
  host = spawn('dotnet', [assembly, '--urls', 'http://127.0.0.1:0'], {
    cwd: fileURLToPath(new URL('./FilePickerHost/', import.meta.url)),
    env: { ...process.env, ASPNETCORE_ENVIRONMENT: 'Development' }, windowsHide: true,
  });
  await new Promise((resolve, reject) => {
    const timeout = setTimeout(() => reject(new Error(`Host did not start: ${output}`)), 45000);
    host.stdout.on('data', data => {
      output += data;
      const match = output.match(/Now listening on: (http:\/\/127\.0\.0\.1:\d+)/);
      if (match) { origin = match[1]; clearTimeout(timeout); resolve(); }
    });
    host.stderr.on('data', data => { output += data; });
    host.on('exit', code => { clearTimeout(timeout); if (!origin) reject(new Error(`Host exited ${code}: ${output}`)); });
  });
  browser = await chromium.launch({ headless: true, ...(process.env.BROWSER_CHANNEL ? { channel: process.env.BROWSER_CHANNEL } : {}) });
});
after(async () => {
  await browser?.close();
  if (host && host.exitCode === null) {
    await new Promise(resolve => { host.once('exit', resolve); host.kill(); });
  }
});

async function submit(page, command) {
  const input = page.locator('.terminal-input');
  await input.waitFor();
  await page.waitForFunction(() => !document.querySelector('.terminal-input')?.disabled);
  await input.fill(command);
  await input.press('Enter');
}
async function content(page, text) {
  await page.waitForFunction(text => document.querySelector('.terminal-output')?.textContent.includes(text), text);
}

for (const width of [390, 1280]) test(`file picker at ${width}px blocks, resumes, caches and cancels`, async () => {
  const page = await browser.newPage({ viewport: { width, height: 900 } });
  page.setDefaultTimeout(15000);
  try {
    await page.goto(origin);
    await page.waitForFunction(() => document.querySelector('.terminal-prompt')?.textContent.includes('>'));
    await submit(page, 'read');
    await page.getByText('Execution is waiting for a file.').waitFor();
    assert.equal(await page.locator('.terminal-input').isDisabled(), true);
    assert.equal((await page.locator('.terminal-output').textContent()).includes('HANDLER STARTED'), false);
    assert.ok(await page.locator('label[for^="cli-file-"]').textContent(), 'Picker must have an accessible label');
    if (width === 1280) {
      const artifacts = new URL('../../.artifacts/', import.meta.url);
      await mkdir(artifacts, { recursive: true });
      await page.screenshot({ path: fileURLToPath(new URL('file-picker.png', artifacts)), fullPage: true });
    }
    await page.locator('input[type=file]').setInputFiles({ name: 'chosen.txt', mimeType: 'text/plain', buffer: Buffer.from('uploaded') });
    await content(page, 'CONTENT: uploaded');
    await submit(page, 'read');
    await page.waitForFunction(() => (document.querySelector('.terminal-output')?.textContent.match(/CONTENT: uploaded/g) || []).length === 2);
    assert.equal(await page.locator('input[type=file]').count(), 0);

    await submit(page, 'read --file missing.txt');
    await page.getByText('Execution is waiting for a file.').waitFor();
    await page.getByRole('button', { name: 'Cancel command' }).click();
    await page.waitForFunction(() => !document.querySelector('.terminal-input').disabled);

    await submit(page, 'dynamic');
    await page.getByText('runtime.txt', { exact: true }).waitFor();
    await page.locator('input[type=file]').setInputFiles({ name: 'too-big.txt', mimeType: 'text/plain', buffer: Buffer.alloc(1024 * 1024 + 1) });
    await page.getByRole('alert').waitFor();
    assert.equal(await page.locator('.terminal-input').isDisabled(), true);
    await page.locator('input[type=file]').setInputFiles({ name: 'runtime.txt', mimeType: 'text/plain', buffer: Buffer.from('runtime upload') });
    await content(page, 'DYNAMIC: runtime upload');

    await submit(page, 'write');
    await content(page, 'FILE WRITTEN');
    await submit(page, 'read --file generated.txt');
    await content(page, 'CONTENT: created');
    assert.equal(await page.locator('input[type=file]').count(), 0);
  } finally { await page.close(); }
});

test('separate browser sessions do not share cached files', async () => {
  const page = await browser.newPage();
  try {
    await page.goto(origin);
    await page.waitForFunction(() => document.querySelector('.terminal-prompt')?.textContent.includes('>'));
    await submit(page, 'read --file generated.txt');
    await page.getByText('Execution is waiting for a file.').waitFor();
    await page.getByRole('button', { name: 'Cancel command' }).click();
  } finally { await page.close(); }
});
