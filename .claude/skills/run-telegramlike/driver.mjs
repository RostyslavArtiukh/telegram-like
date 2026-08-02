// Driver for the running TelegramLike stack (Web BFF at http://localhost:18080).
//
// Drives the Blazor Server UI with Playwright over a real Chromium: registers a
// user, logs in, creates a group chat, sends a message — and (by default) spins up
// a SECOND browser user who joins by chat id and replies, proving the RabbitMQ →
// pubsub → circuit realtime path. Screenshots land in ./screenshots next to this file.
//
// Usage (from the skill dir, after `npm i`):
//   node driver.mjs                 # full flow incl. cross-user realtime
//   node driver.mjs --solo          # single user only (register→login→create→send)
//   BASE_URL=http://localhost:18080 node driver.mjs
//
// Exit code 0 = every assertion passed; non-zero = something failed (message on stderr).

import { chromium } from 'playwright';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
import { mkdirSync } from 'node:fs';

const HERE = dirname(fileURLToPath(import.meta.url));
const SHOTS = join(HERE, 'screenshots');
mkdirSync(SHOTS, { recursive: true });

const BASE_URL = process.env.BASE_URL ?? 'http://localhost:18080';
const SOLO = process.argv.includes('--solo');
const stamp = Date.now();

const log = (m) => console.log(`[driver] ${m}`);
const shot = async (page, name) => {
  const path = join(SHOTS, `${name}.png`);
  await page.screenshot({ path, fullPage: true });
  log(`screenshot → ${path}`);
};

// Blazor prerenders pages before the SignalR circuit attaches; clicking too early
// does a static form post that silently no-ops. So every interactive action retries.
async function retry(label, fn, { tries = 3, waitMs = 8000 } = {}) {
  let lastErr;
  for (let i = 1; i <= tries; i++) {
    try { return await fn(); }
    catch (e) { lastErr = e; log(`${label}: attempt ${i}/${tries} failed (${e.message}); retrying`); await new Promise(r => setTimeout(r, waitMs)); }
  }
  throw new Error(`${label} failed after ${tries} tries: ${lastErr?.message}`);
}

async function register(page, { email, username, displayName, password }) {
  await page.goto(`${BASE_URL}/register`, { waitUntil: 'networkidle' });
  await page.waitForTimeout(2500); // let the circuit attach before typing into MudBlazor fields
  await retry('register', async () => {
    await page.getByPlaceholder('you@example.com').fill(email);
    await page.getByPlaceholder('unique handle').fill(username);
    await page.getByPlaceholder('shown to others').fill(displayName);
    await page.getByPlaceholder('choose a password').fill(password);
    await page.getByRole('button', { name: 'Register' }).click();
    await page.waitForURL('**/login**', { timeout: 10000 }); // Register → /login?registered=true
  });
  log(`registered ${username}`);
}

async function login(page, { email, password }) {
  await page.goto(`${BASE_URL}/login`, { waitUntil: 'networkidle' });
  // Login is a NATIVE <form method="post"> — no circuit needed, but the inputs are
  // MudBlazor so select by placeholder, not by a bootstrap class.
  await page.getByPlaceholder('you@example.com').fill(email);
  await page.getByPlaceholder('Your password').fill(password);
  await page.getByRole('button', { name: 'Sign in' }).click();
  await page.waitForURL(`${BASE_URL}/`, { timeout: 15000 }); // lands on the chats home
  await page.waitForTimeout(2500); // prerender trap: wait for the interactive circuit
  log(`logged in ${email}`);
}

async function createGroup(page, name) {
  return await retry('create group', async () => {
    await page.getByLabel('Group name').fill(name);
    await page.getByRole('button', { name: 'Create group' }).click();
    await page.waitForURL('**/chat/**', { timeout: 10000 }); // navigates straight into the chat
    const id = page.url().split('/chat/')[1];
    log(`created group "${name}" → chat ${id}`);
    return id;
  });
}

async function joinChat(page, chatId) {
  await page.goto(`${BASE_URL}/`, { waitUntil: 'networkidle' });
  await page.waitForTimeout(2500);
  await retry('join chat', async () => {
    await page.getByPlaceholder('00000000-…').fill(chatId);
    await page.getByRole('button', { name: 'Join' }).click();
    await page.waitForURL('**/chat/**', { timeout: 10000 });
  });
  log(`joined chat ${chatId}`);
}

async function sendMessage(page, text) {
  await retry('send message', async () => {
    const composer = page.getByPlaceholder('Type a message…');
    await composer.fill(text);
    await page.getByRole('button', { name: 'Send' }).click();
    await page.getByText(text, { exact: false }).first().waitFor({ timeout: 8000 });
  });
  log(`sent "${text}"`);
}

async function main() {
  const browser = await chromium.launch(); // headless
  const fail = (msg) => { throw new Error(msg); };
  try {
    const u1 = {
      email: `alice_${stamp}@example.com`, username: `alice_${stamp}`,
      displayName: 'Alice', password: 'Passw0rd!',
    };

    const ctx1 = await browser.newContext();
    const page1 = await ctx1.newPage();
    await register(page1, u1);
    await login(page1, u1);
    await shot(page1, '1-home-after-login');

    const chatId = await createGroup(page1, `Room ${stamp}`);
    await sendMessage(page1, 'hello from alice');
    await shot(page1, '2-alice-chat');

    if (SOLO) { log('solo mode: done'); return; }

    // --- Second user: join + reply, assert realtime delivery to Alice's open page ---
    const u2 = {
      email: `bob_${stamp}@example.com`, username: `bob_${stamp}`,
      displayName: 'Bob', password: 'Passw0rd!',
    };
    const ctx2 = await browser.newContext();
    const page2 = await ctx2.newPage();
    await register(page2, u2);
    await login(page2, u2);
    await joinChat(page2, chatId);

    // Bob must see Alice's history (backfill / read path).
    //
    // Joining lands on /chat/{id} within ~20ms, but Bob's membership reaches Messaging's
    // read-model through Chats' outbox — publish plus consume, around a second. Until it
    // lands, Messaging's fail-closed read correctly answers 403 and the page renders empty,
    // and nothing re-fetches it afterwards (only a new message pushes). So waiting on the
    // first render is a coin flip on that window; reload until it has closed.
    await retry('bob sees alice history', async () => {
      await page2.reload({ waitUntil: 'networkidle' });
      await page2.waitForTimeout(2500);
      await page2.getByText('hello from alice', { exact: false }).first().waitFor({ timeout: 5000 });
    }, { tries: 4, waitMs: 2000 }).catch(() => fail('Bob did not see Alice history'));
    log('Bob sees Alice history ✓');
    await sendMessage(page2, 'hi alice, this is bob');
    await shot(page2, '3-bob-chat');

    // The reply must appear on Alice's STILL-OPEN page via the realtime push.
    await page1.getByText('hi alice, this is bob', { exact: false }).first()
      .waitFor({ timeout: 15000 }).catch(() => fail('Realtime: Bob reply never reached Alice page'));
    log('Alice received Bob reply in realtime ✓');
    await shot(page1, '4-alice-realtime');

    log('ALL CHECKS PASSED');
  } finally {
    await browser.close();
  }
}

main().catch((e) => { console.error(`[driver] FAILED: ${e.message}`); process.exit(1); });
