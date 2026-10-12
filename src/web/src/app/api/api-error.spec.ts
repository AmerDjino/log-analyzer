import { HttpErrorResponse } from '@angular/common/http';
import { problems } from '../testing/answers';
import { apiErrorOf } from './api-error';

describe('apiErrorOf', () => {
  it('reads the status and the code from problem details', () => {
    const error = new HttpErrorResponse({ status: 409, error: problems.emailTaken.body });

    expect(apiErrorOf(error)).toEqual({ status: 409, code: 'EMAIL_TAKEN' });
  });

  it("finds no code in nginx's 413 page", () => {
    const error = new HttpErrorResponse({ status: 413, error: problems.nginxTooLarge.body });

    expect(apiErrorOf(error)).toEqual({ status: 413, code: null });
  });
});
