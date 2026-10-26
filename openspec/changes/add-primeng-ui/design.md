# Design: Restyle the web app with PrimeNG

## Context

See proposal.md for the motivation and the specs for the new behavior.

The web app (`src/web`) is Angular 22: standalone components, signals, zoneless change detection.
- Its five pages (sign-in, register, submit, run, runs) use plain HTML elements with a little CSS.
- Global colors are CSS custom properties in `src/styles.css`, written with `light-dark()` under `color-scheme: light dark`, so today the app already follows the device's color preference, but cannot be switched.
- Forms are bound with signals, and each component checks its own input; there is no Angular forms module in use.
- Unit tests (Vitest, jsdom) and the Playwright end-to-end tests find elements by label, role and accessible name: "Email address", "Password", "Paste a log", "Or choose a file", the button "Analyze", the link "Your runs", and the severity filter's `aria-pressed` buttons.
- The production build has an initial bundle budget of 500 kB (warning) and 1 MB (error).

PrimeNG 22.1.x supports Angular `^22.1` and needs `@angular/cdk` `^22.1`. Themes come from `@primeuix/themes`, and icons from `primeicons`.

## Goals / Non-Goals

**Goals:**
- One consistent look across all pages, with every existing label, button name, link name and ARIA state unchanged. The existing tests keep passing with no change, apart from making one end-to-end label lookup exact.
- A theme choice that applies before the first paint, so no light flash shows on a dark choice.

**Non-Goals:**
- Rewriting the pages' logic, signals or API calls. Only templates, styles and the small new behaviors change.
- Server-side rendering, animations beyond what the components bring, or a brand palette.

## Decisions

### PrimeNG in styled mode, with the Aura preset

`providePrimeNG({ theme: { preset: Aura, options: { darkModeSelector: '.app-dark' } } })` goes in `app.config.ts`, and each standalone component imports only the PrimeNG modules it uses, so unused components are tree-shaken. The packages `primeng`, `@primeuix/themes`, `primeicons` and `@angular/cdk` are pinned at exact versions, using the newest 22.1.x of PrimeNG when the work starts.

- Alternative: Angular Material. Rejected: the request names PrimeNG, and Material's form fields would change the markup the tests rely on more.
- Alternative: PrimeNG unstyled mode with Tailwind. Rejected: it adds a second toolkit and all the styling work, for a look Aura already gives.
- Alternative: the Lara or Nora presets. Rejected: Aura is PrimeNG's default and current look; a preset is one line to change later.

### Directives on native elements before wrapper components

Where PrimeNG offers a directive, it styles the existing element: `pButton` on `<button>` and `<a>`, `pInputText` on `<input>`, `pTextarea` on `<textarea>`. Wrapper components are used only where they do not change roles or labels: `p-card`, `p-message`, `p-tag`, `p-table`, `p-toolbar` and the popup `p-menu`.

This keeps `<label for>` pairs, `autocomplete` values, link and button roles, and `aria-pressed`, so the unit and end-to-end tests stay valid.

- Alternative: `p-password` with its `toggleMask`. Rejected: its eye icon is not a labelled button, so it is hard to reach by keyboard and to test, and the component wraps the input in markup of its own.
- Alternative: `p-fileupload`. Rejected: it uploads through a pipeline of its own as multipart. The app must send the file's bytes unchanged, as `text/plain`, through the draft and the API client, and the tests choose files through the "Or choose a file" input.

### The top bar is a toolbar with links, not a menu bar

`p-toolbar` holds the app name, then two `<a pButton routerLink>` links ("Submit a log", "Your runs") with icons, then the theme switch and an account button that opens a popup `p-menu`. The menu's header shows the email address, and its item is "Sign out". The links and the account menu appear only with a session; the sign-in and register pages show the name and the theme switch.

- Alternative: `p-menubar`. Rejected: it renders its items as menu items rather than links, which breaks `getByRole('link', { name: 'Your runs' })`, and two links do not need a collapsing menu.

### Theme: a class on `<html>`, chosen before Angular starts

`main.ts` runs one step before `bootstrapApplication`:
1. It reads the key `log-analyzer.theme` from `localStorage`, inside try/catch.
2. With no stored value, it asks `matchMedia('(prefers-color-scheme: dark)')`.
3. It sets or removes the class `app-dark` on `document.documentElement`.

A small `Theme` service holds the current theme as a signal, toggles the class, and stores the choice, ignoring a storage failure. The switch is an icon button with the accessible name "Dark theme" and `aria-pressed`.

`src/styles.css` sets `color-scheme: light` on `:root` and `color-scheme: dark` on `:root.app-dark`, so the existing `light-dark()` colors (for example, the cited-line highlight) follow the switch as well as PrimeNG's tokens. The global text, surface and border colors map to PrimeNG's tokens (`--p-text-color`, `--p-content-background` and so on), so both systems agree.

- Alternative: keep following the device only. Rejected: the person chose a switch.
- Alternative: store the theme on the server per account. Rejected: it would add an API change, and would not apply on the sign-in page.
- Alternative: an inline script in `index.html`. Rejected: `main.ts` runs before the app renders anything anyway, and it keeps the page free of inline script for a future content security policy.

### Field checks stay in the components, with messages bound to their fields

The sign-in and register components check their signals on submit: required fields, an email shape (`text@text.text` without spaces), and a password of at least 12 characters on register. A field that fails gets `aria-invalid="true"` and `aria-describedby` pointing at a small `p-message` under it.
- No request is sent until every field passes.
- The server's own field errors, which already exist, still show in the same place.

The show-password button sits inside the field (`p-iconfield`), with the accessible names "Show password" and "Hide password" and `aria-pressed`. It switches the input's `type` between `password` and `text`.

- Alternative: Angular reactive forms or signal forms. Rejected: rewriting the forms' state is outside a restyle, and the checks are few.

### Dropping a file goes through the same path as choosing one

The file area is a `<label for="file">` styled as a drop zone around the existing input. It shows "Drop a log file here or choose one", the size limit from `GET /runs/limits`, and the chosen file's name and size.
- `dragover` prevents the default action and marks the zone.
- `drop` takes `dataTransfer.files`: exactly one file goes to the same method the input's `change` uses, so the size check and the draft apply.
- Two or more files show "One file can be submitted at a time" in the existing field-error slot, and choose nothing.

### Unit tests run with PrimeNG in jsdom

jsdom has no `matchMedia`, which PrimeNG and the theme service read, so a test setup file defines a stub that reports a light-color device. Tests can override it to prefer dark. `testing/test-app.ts` adds `providePrimeNG` with the same preset, so tests render what the app renders.

The end-to-end helper finds the password field with `getByLabel('Password', { exact: true })`, because "Show password" now also contains "Password".

### The bundle budget is measured, not guessed

PrimeNG, the Aura preset and the icon font add to the first load. The build is measured after the change. If the initial bundle passes 500 kB, the warning budget is raised to the measured size plus about 10%, and the 1 MB error budget stays. The icon font loads through `angular.json`'s `styles` (`primeicons/primeicons.css`).

## Risks / Trade-offs

- [PrimeNG 22.1 is new and changes often] → Exact versions; the unit and end-to-end tests check behavior, not PrimeNG's markup.
- [Component styles override the old CSS in surprising places] → The old page CSS is removed as each page moves to PrimeNG, rather than layered on top.
- [The initial bundle passes 1 MB] → Import per component, never a barrel module. If it still passes, stop and revisit the component choice with the user.
- [A dark choice stored, then the storage cleared] → The next visit follows the device again, which the spec allows.

## Migration Plan

Frontend only. No API, data or configuration change; the web image is rebuilt as usual. Rolling back means reverting the change.
