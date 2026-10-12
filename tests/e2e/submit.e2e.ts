import { expect, test } from '@playwright/test';
import { signUpAndSignIn, threeLineLog } from './accounts';

// Files are chosen with the type application/octet-stream. A page that sent a file with its own type, rather than an
// explicit text/plain, would then get routing's 415, and these tests would fail.

test('[analysis-ui/file-that-is-not-utf-8-is-explained] explains a file with invalid UTF-8 and creates no run', async ({
  page,
}) => {
  await signUpAndSignIn(page, 'utf8');

  await page.getByLabel('Or choose a file').setInputFiles({
    name: 'not-utf-8.log',
    mimeType: 'application/octet-stream',
    buffer: Buffer.from([0x66, 0x6f, 0xff, 0x0a]),
  });
  await page.getByRole('button', { name: 'Analyze' }).click();
  await expect(page.getByText('This file is not a UTF-8 text log')).toBeVisible();

  // The run list's own answer shows that no run was created. The account is fresh, so any item would be this file's.
  // The path must be exactly /api/runs, so that GET /api/runs/limits does not count.
  const list = page.waitForResponse(
    (response) => new URL(response.url()).pathname === '/api/runs' && response.request().method() === 'GET',
  );
  await page.getByRole('link', { name: 'Your runs' }).click();
  expect((await (await list).json()).items).toEqual([]);
});

test("[analysis-ui/chosen-file-keeps-its-name] lists the new run with the chosen file's name", async ({ page }) => {
  await signUpAndSignIn(page, 'name');

  await page.getByLabel('Or choose a file').setInputFiles({
    name: 'orders-api.log',
    mimeType: 'application/octet-stream',
    buffer: Buffer.from(threeLineLog),
  });
  await page.getByRole('button', { name: 'Analyze' }).click();
  await expect(page).toHaveURL(/\/runs\/[0-9a-f-]{36}$/);
  await page.getByRole('link', { name: 'Your runs' }).click();

  await expect(page.getByRole('link', { name: 'orders-api.log' })).toBeVisible();
});
