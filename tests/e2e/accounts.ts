import { Page, expect } from '@playwright/test';

export const password = 'correct horse battery';

/**
 * Three lines, the second an error, so the stand-in model returns one finding: "Error logged on line 2".
 */
export const threeLineLog =
  '2026-10-10T09:00:01Z info: Orders.Api[0] Application started\n' +
  '2026-10-10T09:00:04Z fail: Orders.Api[0] Payment for order 42 failed\n' +
  '2026-10-10T09:00:05Z info: Orders.Api[0] Request finished\n';

/**
 * Creates a fresh account through the API, so tests that share the stack's database never see each other's runs,
 * then signs in through the sign-in page. Returns the email address.
 */
export async function signUpAndSignIn(page: Page, name: string): Promise<string> {
  const email = `e2e-${name}-${Date.now()}-${Math.random().toString(36).slice(2, 8)}@example.com`;
  const registered = await page.request.post('/api/auth/register', { data: { email, password } });
  expect(registered.status()).toBe(201);

  await page.goto('/sign-in');
  await fillSignIn(page, email);
  await expect(page.getByRole('heading', { name: 'Submit a log' })).toBeVisible();
  return email;
}

/** Signs in on the sign-in page that is already open, without loading the page again. */
export async function fillSignIn(page: Page, email: string): Promise<void> {
  await page.getByLabel('Email address').fill(email);
  await page.getByLabel('Password').fill(password);
  await page.getByRole('button', { name: 'Sign in' }).click();
}
