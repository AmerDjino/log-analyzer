# Tasks

## 1. PrimeNG and the test setup

- [ ] 1.1 On a new branch `add-primeng-ui` from a `main` that includes this plan, add the packages and setup listed below; verify `npm ci` succeeds from an empty `node_modules`, `npm test -- --watch=false` passes unchanged, and `npm run build` passes, then note the initial bundle size
  - `primeng` (newest 22.1.x), `@primeuix/themes`, `primeicons` and `@angular/cdk`, at exact versions, with no `--force` or `--legacy-peer-deps`.
  - `providePrimeNG` with the Aura preset and `darkModeSelector: '.app-dark'` in `app.config.ts`.
  - `primeicons/primeicons.css` in `angular.json`'s `styles`.
  - The `matchMedia` stub as a test setup file, registered in the `test` options.
  - `providePrimeNG` in `testing/test-app.ts`.
  - If PrimeNG does not install or build with Angular 22.2 and TypeScript 6.0, stop and revisit the design with the user.
- [ ] 1.2 If the initial bundle passes the 500 kB warning, raise only the warning budget to the measured size plus about 10%; verify `npm run build` passes with no budget warning, and the 1 MB error budget is unchanged

## 2. The theme switch

- [ ] 2.1 Write the failing tests below, with a `Theme` service stub that compiles; verify each fails on its assertions
  - `theme/theme.spec.ts` for its two scenarios, and its check under "Tests without a scenario".
  - `tests/e2e/theme.e2e.ts` for its two scenarios. Verify these fail against the stack built from `main`.
- [ ] 2.2 Implement the theme as listed below; verify `theme.spec.ts` passes, and then the whole web suite
  - In `main.ts`: read the stored choice (try/catch), fall back to `prefers-color-scheme`, and set `app-dark` on `<html>` before `bootstrapApplication`.
  - The `Theme` service: a signal, a toggle that sets the class and stores the choice, and ignores a storage failure.
  - In `src/styles.css`: `color-scheme: light` on `:root` and `dark` on `:root.app-dark`, and the global colors mapped to PrimeNG's tokens.
- [ ] 2.3 Rebuild the stack and run `theme.e2e.ts`; verify both theme scenarios pass

## 3. The top bar

- [ ] 3.1 Write the failing `app.spec.ts` tests for the three top-bar scenarios and the theme switch's name and `aria-pressed`; verify each fails on its assertions
- [ ] 3.2 Replace the header with `p-toolbar`, as listed below; verify the `app.spec.ts` tests and the whole web suite pass
  - The app name, then `<a pButton routerLink>` links "Submit a log" and "Your runs" with icons, shown only with a session.
  - The theme switch, always shown.
  - An account button that opens a popup `p-menu`, whose header shows the email address and whose item is "Sign out", calling the existing sign-out. A sign-out error still shows.

## 4. The sign-in and account creation cards

- [ ] 4.1 Write the failing tests below; verify each fails on its assertions, and that the existing sign-in and register tests still pass
  - `sign-in.spec.ts` for its three scenarios.
  - `register.spec.ts` for its two scenarios.
- [ ] 4.2 Restyle both pages and add the checks, as listed below; verify `sign-in.spec.ts`, `register.spec.ts` and the whole web suite pass
  - Each page is a centered `p-card` with `pInputText` inputs, keeping the labels, ids and `autocomplete` values.
  - The field checks on submit, with `aria-invalid`, and `aria-describedby` pointing at a `p-message` under the field.
  - The show-password button in a `p-iconfield`, named "Show password" or "Hide password", with `aria-pressed`.
  - The server's field errors keep their places, and the links between the two pages stay.
- [ ] 4.3 Make `tests/e2e/accounts.ts` find the password with `getByLabel('Password', { exact: true })`; verify it still signs up and signs in against the stack

## 5. The submit form

- [ ] 5.1 Write the failing `submit.spec.ts` tests for the two drop scenarios, and the size-limit and drag-marking checks; verify each fails on its assertions
- [ ] 5.2 Restyle the submit page, as listed below; verify `submit.spec.ts` and the whole web suite pass, and the "Paste a log" and "Or choose a file" labels and the "Analyze" button keep their names
  - A `p-card` with a `pTextarea` paste area.
  - A drop zone label around the existing file input, showing the size limit and the chosen file.
  - `dragover`/`drop` handlers that pass one file to the existing choose path, and refuse two or more with "One file can be submitted at a time".
  - A `pButton` Analyze button.

## 6. The run and run list pages

- [ ] 6.1 Restyle both pages, as listed below, removing the old page CSS that PrimeNG replaces; verify `run.spec.ts`, `runs.spec.ts`, `evidence.spec.ts` and the whole web suite pass unchanged
  - Status and severity as `p-tag`s.
  - The severity filter as `pButton` toggle buttons, keeping `aria-pressed`.
  - Each finding as a panel, with its show-the-cited-lines button styled.
  - The run list as a `p-table` whose file names stay links.
  - The evidence highlight keeps working in both themes.

## 7. Docs and integration checks

- [ ] 7.1 Describe in `src/web/README.md` the component library, the Aura theme, the theme switch and its storage key, and the rule of directives on native elements before wrapper components; verify the documented commands run as written
- [ ] 7.2 Run every gate, as listed below; verify all pass
  - `npm test -- --watch=false` and `npm run build` in `src/web`, with no budget warning beyond the one raised in 1.2.
  - `openspec validate --all --strict`.
  - `node scripts/check-scenario-coverage.mjs`.
  - The Docker stack from `.env.example`, with every Playwright test.
  - A look at each page in both themes, at desktop and phone width.

## Workflow follow-up

- Review the whole branch once, then run `/opsx:verify add-primeng-ui` and fix what either finds in the same pull request.
- Archive the change in that pull request, then open it for the user to merge.
