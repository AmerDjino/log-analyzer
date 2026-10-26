# Proposal

## Why

The web interface works but looks unfinished: plain browser inputs, unstyled buttons and a bare header. A consistent component library makes the app look like a product and easier to use, and the account pages, the submit form and the navigation are the first things a person sees. The interface has its first complete flow since `add-web-shell` and real analyses since `add-claude-integration`, so it is a good time to give it a proper look before more screens arrive.

## What Changes

- Restyle every page with the free PrimeNG component library and one theme: buttons, inputs, cards, messages, tags and tables in a consistent style.
- Give sign-in and account creation each a centered card of their own (they stay two pages with their own addresses):
  - fields are checked before anything is sent, with the message next to the field;
  - password fields get a button that shows or hides the typed characters.
- Turn the submit page into a proper form card: a paste area, a file area that also accepts a dropped file, the size limit shown up front, and a clear Analyze button.
- Replace the header with a top bar: the app name, links to Submit a log and Your runs, a light/dark theme switch, and an account menu holding the email address and Sign out.
- Add a light and a dark theme. The first visit follows the system setting, and a choice made with the switch is remembered in the browser.
- Show run statuses and finding severities as colored tags, the run list as a table, the severity filter as a row of toggle buttons, and each finding as a panel.

Assumptions recorded here, not asked: the theme is PrimeNG's Aura preset with its default colors; every existing label, button name and link name stays the same, so existing tests and habits keep working.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `analysis-ui`: adds requirements for the top bar, the theme switch, showing a typed password, checking account fields before sending, and choosing a file by dropping it. Existing requirements and their scenarios do not change.

## Non-goals

- No new pages or features beyond the ones above: no settings page, profile page, password reset or insights.
- No change to the API, its error codes or any backend behavior.
- No new toggles besides the theme switch and the show-password button: no paste/upload switch on the submit form. The run page's existing severity filter and its show-the-cited-lines toggle keep their behavior and are only restyled.
- No custom design system or brand colors; the theme's defaults are used.
- No paid PrimeNG add-ons (PrimeBlocks, premium templates or themes).
- No change to how sessions, drafts or polling work.

## Impact

- **Web app (`src/web`)**: new dependencies `primeng`, `@primeuix/themes`, `primeicons` and `@angular/cdk`, at exact versions; every page template and its styles; the app configuration; global styles; unit tests for the new behavior.
- **End-to-end tests (`tests/e2e`)**: the password field is found by its exact label, because the new show-password button also mentions "password". No other test needs to change.
- **Build**: the app's first-load bundle grows; the budget in `angular.json` is checked, and raised only as far as needed.
- **Docs**: `src/web/README.md` describes the theme, the component library and the theme switch.
- **No model calls are touched**, so there is no token or cost impact.
