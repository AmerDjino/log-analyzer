# Test Plan

All 14 scenarios are deterministic.
- **Unit tests (Vitest, jsdom):** most scenarios. They render the real pages with the app's routes and PrimeNG, against `HttpTestingController`, as the existing web specs do.
- **End-to-end tests (Playwright, Chromium):** the two theme scenarios that depend on a real browser's color preference and storage across a reload. They run against the Docker Compose stack in the CI stack job.

Each test carries its scenario id in brackets in its name.

| Scenario id | Test | Kind | Project |
|---|---|---|---|
| `analysis-ui/top-bar-on-a-signed-in-page` | `app.spec.ts`: `[analysis-ui/top-bar-on-a-signed-in-page] shows the links and an account menu with the email address` | unit | src/web |
| `analysis-ui/sign-out-from-the-account-menu` | `app.spec.ts`: `[analysis-ui/sign-out-from-the-account-menu] signs out from the account menu and shows sign-in` | unit | src/web |
| `analysis-ui/no-navigation-before-signing-in` | `app.spec.ts`: `[analysis-ui/no-navigation-before-signing-in] shows only the name and the theme switch on sign-in` | unit | src/web |
| `analysis-ui/first-visit-follows-the-device` | `theme.e2e.ts`: `[analysis-ui/first-visit-follows-the-device] shows the dark theme on a dark device with nothing stored` | e2e | tests/e2e |
| `analysis-ui/switching-the-theme` | `theme.spec.ts`: `[analysis-ui/switching-the-theme] switches to dark without a reload` | unit | src/web |
| `analysis-ui/chosen-theme-is-remembered` | `theme.e2e.ts`: `[analysis-ui/chosen-theme-is-remembered] keeps the dark choice after a reload on a light device` | e2e | tests/e2e |
| `analysis-ui/theme-switch-works-without-browser-storage` | `theme.spec.ts`: `[analysis-ui/theme-switch-works-without-browser-storage] switches the theme when storage throws` | unit | src/web |
| `analysis-ui/password-is-hidden-when-the-page-opens` | `sign-in.spec.ts`: `[analysis-ui/password-is-hidden-when-the-page-opens] hides the typed password` | unit | src/web |
| `analysis-ui/show-and-hide-the-password` | `register.spec.ts`: `[analysis-ui/show-and-hide-the-password] shows the typed password, then hides it again` | unit | src/web |
| `analysis-ui/malformed-email-address-is-explained-before-sending` | `sign-in.spec.ts`: `[analysis-ui/malformed-email-address-is-explained-before-sending] explains the email address and sends nothing` | unit | src/web |
| `analysis-ui/empty-fields-are-explained-before-sending` | `sign-in.spec.ts`: `[analysis-ui/empty-fields-are-explained-before-sending] explains both empty fields and sends nothing` | unit | src/web |
| `analysis-ui/short-password-is-explained-before-sending` | `register.spec.ts`: `[analysis-ui/short-password-is-explained-before-sending] explains the short password and sends nothing` | unit | src/web |
| `analysis-ui/dropped-file-is-chosen` | `submit.spec.ts`: `[analysis-ui/dropped-file-is-chosen] chooses a dropped file and enables Analyze` | unit | src/web |
| `analysis-ui/several-dropped-files-are-refused` | `submit.spec.ts`: `[analysis-ui/several-dropped-files-are-refused] refuses two dropped files and chooses none` | unit | src/web |

## Fixtures

- **Unit:**
  - A test setup file defines `window.matchMedia`, which jsdom lacks. It reports a light-color device unless a test sets it to dark. It is registered in `angular.json`'s `test` options.
  - `testing/test-app.ts` provides PrimeNG with the Aura preset and the `.app-dark` selector, as `app.config.ts` does.
  - **Storage:** each theme test clears `localStorage` before and after. The no-storage test replaces `localStorage.setItem` and `getItem` with functions that throw, and restores them afterwards.
  - **Dropped files:** a `drop` event built with a `DataTransfer`-like object holding one `File` ("orders-api.log", three lines) or two. jsdom has no `DataTransfer` constructor, so the test defines the `dataTransfer` property on the event.
  - **No request sent:** `HttpTestingController.expectNone` on the sign-in and register endpoints.
  - Existing answers in `testing/answers.ts`: the signed-in account `ana@example.com`, the limits, and the run list.
- **End-to-end:**
  - `theme.e2e.ts` uses Playwright's `colorScheme` option (`dark` for the first visit, `light` for the remembered choice) on a fresh browser context with empty storage.
  - It needs no account, because the sign-in page shows the theme switch.
  - It reads the theme from the class `app-dark` on `<html>`.

## Tests without a scenario

- `theme.spec.ts`: the theme applied in `main.ts` before the app starts matches what the `Theme` service reports when it is created.
- `submit.spec.ts`: the file area shows the size limit from `GET /runs/limits`, and a dragged file marks the zone until it leaves.
- `sign-in.spec.ts`, `register.spec.ts`: the server's own field errors still show at their fields, beside the new checks (the existing tests keep passing).
- `app.spec.ts`: the theme switch's accessible name is "Dark theme", and `aria-pressed` follows the theme.
- **Every existing web unit test and end-to-end test passes**, with only `accounts.ts`'s password lookup made exact.
- **The production build passes its budgets** (`npm run build`).
