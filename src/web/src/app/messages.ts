// Plain-language messages for the API's error codes and a failed run's reasons. The API's own titles and details are
// written for developers; the code is the contract.
import { ApiError, ApiErrorCode } from './api/api-error';

/** A form field an error belongs to. */
export type FormField = 'email' | 'password';

export interface ErrorMessage {
  text: string;
  field?: FormField;
}

// A record over every code, so a code the API adds fails the build until it has a message here.
export const errorMessages: Record<ApiErrorCode, ErrorMessage> = {
  INVALID_EMAIL: { text: 'Enter a valid email address of at most 254 characters.', field: 'email' },
  PASSWORD_TOO_SHORT: { text: 'A password needs at least 12 characters.', field: 'password' },
  PASSWORD_TOO_LONG: { text: 'A password can have at most 128 characters.', field: 'password' },
  EMAIL_TAKEN: { text: 'An account with this email address already exists.', field: 'email' },
  INVALID_CREDENTIALS: { text: 'The email address or password is incorrect.' },
  ACCOUNT_LOCKED: {
    text: 'Sign-in to this account is blocked for up to 15 minutes after too many failed attempts.',
  },
  SIGN_IN_REQUIRED: { text: 'Your session has ended. Sign in again.' },
  UNSUPPORTED_CHARSET: { text: 'The log must be sent as UTF-8 text.' },
  INPUT_TOO_LARGE: { text: 'The log is larger than the size limit.' },
  UNSUPPORTED_CONTENT: { text: 'This file is not a UTF-8 text log. Choose a text log file.' },
  EMPTY_INPUT: { text: 'The log is empty. Paste or choose a log that has some text in it.' },
  RUN_NOT_FOUND: { text: 'There is no run at this address.' },
  INVALID_LIMIT: { text: 'The run list could not be read. Reload the page.' },
  INVALID_CURSOR: { text: 'The run list could not be read. Reload the page.' },
};

export const generalErrorMessage = 'The request failed. Try again in a moment.';

/**
 * The message for a failed request. A 413 always gets the size message, with or without a code, because nginx answers
 * a body over its own limit with an HTML page that has none.
 */
export function messageFor(error: ApiError, maxSubmissionBytes?: number): ErrorMessage {
  if (error.status === 413) {
    return {
      text:
        maxSubmissionBytes === undefined
          ? errorMessages.INPUT_TOO_LARGE.text
          : `The log is larger than the ${formatSize(maxSubmissionBytes)} limit.`,
    };
  }

  return error.code !== null && Object.hasOwn(errorMessages, error.code)
    ? errorMessages[error.code as ApiErrorCode]
    : { text: generalErrorMessage };
}

const failureReasons: Record<string, string> = {
  VALIDATION_FAILED:
    "The model's answers did not pass validation, so nothing from them was kept. Submit the log again to retry.",
  MODEL_UNAVAILABLE: 'The model service could not be reached. Submit the log again later.',
  MODEL_CONFIGURATION_ERROR:
    "The model service refused the API's request. The API's configuration needs to be fixed.",
  INTERRUPTED: 'The API stopped while it was analyzing this log. Submit the log again.',
  INTERNAL_ERROR:
    'Something went wrong while this log was analyzed. The server log has the details.',
};

/** A plain-language explanation of why a run failed. */
export function failureReasonMessage(reason: string | null): string {
  return (reason !== null && failureReasons[reason]) || 'The analysis failed.';
}

const megabyte = 1_048_576;

/** A size in MB of 1,048,576 bytes, with up to two decimals, as the API writes it; smaller sizes in KB. */
export function formatSize(bytes: number): string {
  if (bytes >= megabyte) {
    return `${Number((bytes / megabyte).toFixed(2))} MB`;
  }

  return bytes >= 1024 ? `${Number((bytes / 1024).toFixed(2))} KB` : `${bytes} bytes`;
}
