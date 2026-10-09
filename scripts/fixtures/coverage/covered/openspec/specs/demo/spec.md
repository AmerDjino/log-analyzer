# Demo Specification

## Purpose

A minimal capability used to test the scenario coverage check.

## Requirements

### Requirement: Greeting
The system SHALL greet the user by name.

#### Scenario: Greeting is shown
- **GIVEN** a user named Ada
- **WHEN** the page loads
- **THEN** it says "Hello, Ada"

#### Scenario: [eval] Reply sounds friendly
- **GIVEN** a user who says hello
- **WHEN** the assistant replies
- **THEN** the reply reads as friendly

### Requirement: Banner
The page SHALL show the product name in its banner.

#### Scenario: Banner shows the product name
- **WHEN** the page loads
- **THEN** the banner reads "Demo"
