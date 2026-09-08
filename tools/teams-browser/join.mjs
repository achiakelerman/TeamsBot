import { chromium } from 'playwright';
import { mkdir, appendFile } from 'node:fs/promises';

const meetingUrl = process.argv[2];
const displayName = process.argv[3] ?? 'Meeting Companion';
if (!meetingUrl) {
  console.error('Usage: npm run join -- <TEAMS_MEETING_URL> [display-name]');
  process.exit(2);
}

await mkdir('output/playwright', { recursive: true });
const output = `output/playwright/${new Date().toISOString().replaceAll(':', '-')}.jsonl`;
const executablePath = process.env.TEAMS_BROWSER_PATH ?? 'C:/Program Files/Google/Chrome/Application/chrome.exe';
const context = await chromium.launchPersistentContext('playwright-profile', { headless: false, executablePath, permissions: [] });
const page = context.pages()[0] ?? await context.newPage();
await page.goto(meetingUrl, { waitUntil: 'domcontentloaded' });

const continueButton = page.getByRole('button', { name: /join on the web|continue on this browser/i });
if (await continueButton.isVisible({ timeout: 15000 }).catch(() => false)) await continueButton.click();

const nameInput = page.locator('input[placeholder="Type your name"]');
if (await nameInput.isVisible({ timeout: 30000 }).catch(() => false)) {
  await nameInput.fill(displayName);
  await page.getByRole('button', { name: /join now/i }).click();
}

console.log('Browser opened. Complete Microsoft sign-in or lobby admission manually if requested.');
console.log(`Transcript output: ${output}`);
const captionRoot = page.locator('[data-tid="closed-caption-renderer-wrapper"]');
if (await captionRoot.isVisible({ timeout: 120000 }).catch(() => false)) {
  await page.exposeFunction('emitCaption', async (caption) => {
    await appendFile(output, JSON.stringify(caption) + '\n');
    console.log(`${caption.participant}: ${caption.text}`);
  });
  await page.evaluate(() => {
    const root = document.querySelector('[data-tid="closed-caption-renderer-wrapper"]');
    if (!root) return;
    const observer = new MutationObserver(() => {
      for (const node of root.querySelectorAll('.fui-ChatMessageCompact')) {
        const author = node.querySelector('[data-tid="author"]')?.textContent?.trim() ?? 'Unknown';
        const text = node.querySelector('[data-tid="closed-caption-text"]')?.textContent?.trim() ?? '';
        if (text) window.emitCaption({ participant: author, text, timestamp: new Date().toISOString() });
      }
    });
    observer.observe(root, { childList: true, subtree: true });
  });
}
await new Promise(() => {});
