import { failureReasons } from './testing/answers';
import {
  errorMessages,
  failureReasonMessage,
  formatSize,
  generalErrorMessage,
  messageFor,
} from './messages';

describe('messages', () => {
  it('explains every failure reason', () => {
    const fallback = failureReasonMessage('A_REASON_THIS_APP_DOES_NOT_KNOW');

    for (const reason of failureReasons) {
      expect(failureReasonMessage(reason), reason).not.toBe('');
      expect(failureReasonMessage(reason), reason).not.toBe(fallback);
    }
  });

  it('writes sizes in MB of 1,048,576 bytes with up to two decimals', () => {
    expect(formatSize(1_048_576)).toBe('1 MB');
    expect(formatSize(1_572_864)).toBe('1.5 MB');
    expect(formatSize(1_500_000)).toBe('1.43 MB');
  });

  it('ties the account codes to their fields', () => {
    expect(errorMessages.INVALID_EMAIL.field).toBe('email');
    expect(errorMessages.EMAIL_TAKEN.field).toBe('email');
    expect(errorMessages.PASSWORD_TOO_SHORT.field).toBe('password');
    expect(errorMessages.PASSWORD_TOO_LONG.field).toBe('password');
  });

  it('gives an answer without a known code the general message', () => {
    expect(generalErrorMessage).not.toBe('');
    expect(messageFor({ status: 400, code: null }).text).toBe(generalErrorMessage);
    expect(messageFor({ status: 500, code: null }).text).toBe(generalErrorMessage);
    expect(messageFor({ status: 400, code: 'A_CODE_THIS_APP_DOES_NOT_KNOW' }).text).toBe(
      generalErrorMessage,
    );
  });
});
