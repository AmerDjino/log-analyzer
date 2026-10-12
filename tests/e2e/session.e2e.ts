import { expect, test } from '@playwright/test';
import { fillSignIn, signUpAndSignIn, threeLineLog } from './accounts';

test("[analysis-ui/signed-out-person-is-sent-to-sign-in-and-back] sends a signed-out visit to a run page to sign-in, then back to that run's page with its finding", async ({
  page,
  browser,
  baseURL,
}) => {
  const email = await signUpAndSignIn(page, 'back');
  await page.getByLabel('Paste a log').fill(threeLineLog);
  await page.getByRole('button', { name: 'Analyze' }).click();
  await expect(page).toHaveURL(/\/runs\/[0-9a-f-]{36}$/);
  const runPath = new URL(page.url()).pathname;

  // A new browser context has neither the session cookie nor the app's in-memory account, so the guard decides.
  const context = await browser.newContext({ baseURL });
  const signedOut = await context.newPage();
  await signedOut.goto(runPath);
  await expect(signedOut).toHaveURL(/\/sign-in\?returnUrl=/);
  await fillSignIn(signedOut, email);

  await expect(signedOut).toHaveURL(new RegExp(`${runPath}$`));
  await expect(signedOut.getByText('Error logged on line 2')).toBeVisible();
  await context.close();
});

test('[analysis-ui/pasted-log-survives-an-ended-session] keeps the pasted log through sign-in after the session cookie is gone', async ({
  page,
  context,
}) => {
  const email = await signUpAndSignIn(page, 'ended');
  await page.getByLabel('Paste a log').fill(threeLineLog);

  await context.clearCookies();
  await page.getByRole('button', { name: 'Analyze' }).click();
  await expect(page).toHaveURL(/\/sign-in\?returnUrl=%2F&ended=1$/);
  await expect(page.getByText('Your session has ended')).toBeVisible();
  await fillSignIn(page, email);

  await expect(page.getByRole('heading', { name: 'Submit a log' })).toBeVisible();
  await expect(page.getByLabel('Paste a log')).toHaveValue(threeLineLog);
});
