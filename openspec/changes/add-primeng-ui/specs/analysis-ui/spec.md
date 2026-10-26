# Spec Delta

## ADDED Requirements

### Requirement: Navigate from a top bar
Every page that needs a session SHALL show a top bar with the app's name, a link to submit a log, a link to the person's runs, the theme switch and an account menu. The account menu SHALL show the signed-in email address and hold the sign-out action. The sign-in and account creation pages MUST show the app's name and the theme switch, but no links and no account menu.

#### Scenario: Top bar on a signed-in page
- **GIVEN** a person signed in as "ana@example.com"
- **WHEN** the submit page is shown
- **THEN** the top bar has the links "Submit a log" and "Your runs"
- **AND** opening the account menu shows "ana@example.com" and a "Sign out" action

#### Scenario: Sign-out from the account menu
- **GIVEN** a signed-in person on the run list
- **WHEN** they choose "Sign out" in the account menu
- **THEN** the sign-in page is shown

#### Scenario: No navigation before signing in
- **GIVEN** a person without a session
- **WHEN** the sign-in page is shown
- **THEN** the top bar shows the app's name and the theme switch
- **AND** it has no "Your runs" link and no account menu

### Requirement: Switch between light and dark themes
The interface SHALL offer a switch between a light and a dark theme on every page. On a first visit it MUST follow the device's color preference. A choice made with the switch MUST be remembered in the browser and applied on later visits, before the page is shown. When the browser cannot store the choice, the switch MUST still work for the visit.

#### Scenario: First visit follows the device
- **GIVEN** a browser with no stored theme whose device prefers dark colors
- **WHEN** a person opens the sign-in page
- **THEN** the dark theme is shown

#### Scenario: Switching the theme
- **GIVEN** the light theme is shown
- **WHEN** the person turns the theme switch to dark
- **THEN** the dark theme is shown without reloading the page

#### Scenario: Chosen theme is remembered
- **GIVEN** a person chose the dark theme on a device that prefers light colors
- **WHEN** they open the app again in the same browser
- **THEN** the dark theme is shown

#### Scenario: Theme switch works without browser storage
- **GIVEN** a browser that refuses to store data for the app
- **WHEN** a person turns the theme switch to dark
- **THEN** the dark theme is shown for the rest of the visit

### Requirement: Show a typed password
Every password field SHALL have a button that shows the typed characters and, pressed again, hides them. The password MUST be hidden whenever the page opens.

#### Scenario: Password is hidden when the page opens
- **GIVEN** the sign-in page
- **WHEN** a person types a password
- **THEN** the field shows the password as hidden characters

#### Scenario: Show and hide the password
- **GIVEN** a person has typed a password on the account creation page
- **WHEN** they press the show-password button
- **THEN** the field shows the typed characters
- **AND** pressing the button again hides them

### Requirement: Check account fields before sending
The sign-in and account creation forms SHALL check their fields before sending anything: an empty or malformed email address, an empty password and, when creating an account, a password shorter than 12 characters MUST be explained next to the field concerned, and no request MUST be sent until every field passes. The values the person entered MUST stay in the fields.

#### Scenario: Malformed email address is explained before sending
- **GIVEN** a person enters "ana.example.com" and a password on the sign-in page
- **WHEN** they sign in
- **THEN** a message at the email field says that the email address is not valid
- **AND** no sign-in request is sent
- **AND** "ana.example.com" is still in the email field

#### Scenario: Empty fields are explained before sending
- **GIVEN** the sign-in page with both fields empty
- **WHEN** a person signs in
- **THEN** a message at the email field and a message at the password field say that each is required
- **AND** no sign-in request is sent

#### Scenario: Short password is explained before sending
- **GIVEN** a person enters a new email address and an 11-character password on the account creation page
- **WHEN** they create the account
- **THEN** a message at the password field says that a password needs at least 12 characters
- **AND** no account creation request is sent

### Requirement: Drop a file to choose it
The submit form SHALL accept a log file dropped onto its file area as if it had been chosen, with the same size check and the same name kept. Dropping more than one file MUST choose none and say that one file can be submitted at a time.

#### Scenario: Dropped file is chosen
- **GIVEN** the submit page with an empty paste area
- **WHEN** a person drops the file "orders-api.log" onto the file area
- **THEN** the form shows "orders-api.log" with its size as the chosen file
- **AND** the submit button is enabled

#### Scenario: Several dropped files are refused
- **GIVEN** the submit page
- **WHEN** a person drops two files onto the file area at once
- **THEN** a message says that one file can be submitted at a time
- **AND** no file is chosen
