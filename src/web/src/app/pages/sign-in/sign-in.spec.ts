import { TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';
import { Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { Account } from '../../api/api-client';
import { ErrorAnswer, ana, problems } from '../../testing/answers';
import { answerSession, flushError, provideTestApp, typeInto } from '../../testing/test-app';

describe('SignInPage', () => {
  let http: HttpTestingController;
  let harness: RouterTestingHarness;

  beforeEach(async () => {
    TestBed.configureTestingModule({ providers: provideTestApp() });
    http = TestBed.inject(HttpTestingController);
    harness = await RouterTestingHarness.create();
  });

  afterEach(() => http.verify());

  async function open(url: string, account: Account | null): Promise<HTMLElement> {
    const navigation = harness.navigateByUrl(url);
    await answerSession(http, account);
    await navigation;
    return harness.routeNativeElement!;
  }

  async function signIn(page: HTMLElement, email: string, password: string): Promise<void> {
    typeInto(page, '#email', email);
    typeInto(page, '#password', password);
    page.querySelector('form')!.dispatchEvent(new Event('submit'));
    await harness.fixture.whenStable();
  }

  async function failSignIn(page: HTMLElement, answer: ErrorAnswer): Promise<void> {
    await signIn(page, 'ana@example.com', 'wrong password!');
    flushError(http.expectOne('/api/auth/sign-in'), answer);
    await harness.fixture.whenStable();
  }

  it('[analysis-ui/failed-sign-in-keeps-the-email-address] says the email address or password is incorrect and keeps the email address', async () => {
    const page = await open('/sign-in', null);

    await failSignIn(page, problems.invalidCredentials);

    expect(page.textContent).toContain('The email address or password is incorrect');
    expect(page.querySelector<HTMLInputElement>('#email')!.value).toBe('ana@example.com');
  });

  it('[analysis-ui/locked-account-is-explained] says sign-in is blocked for up to 15 minutes', async () => {
    const page = await open('/sign-in', null);

    await failSignIn(page, problems.accountLocked);

    expect(page.textContent).toContain(
      'blocked for up to 15 minutes after too many failed attempts',
    );
  });

  it('shows a request error in an aria-live region', async () => {
    const page = await open('/sign-in', null);

    await failSignIn(page, problems.serviceUnavailable);

    const live = page.querySelector('[aria-live]');
    expect(live?.textContent).toContain('The request failed');
  });

  it('sends a signed-in person who opens /sign-in to /', async () => {
    await open('/sign-in', ana);

    expect(TestBed.inject(Router).url).toBe('/');
  });

  it('says that the session has ended when ended=1', async () => {
    const page = await open('/sign-in?ended=1', null);

    expect(page.textContent).toContain('Your session has ended');
  });

  it.each(['//evil.example', '/\\evil.example', 'https://evil.example'])(
    'opens / after signing in with returnUrl %s',
    async (returnUrl) => {
      const page = await open(`/sign-in?returnUrl=${encodeURIComponent(returnUrl)}`, null);

      await signIn(page, 'ana@example.com', 'correct horse battery');
      http.expectOne('/api/auth/sign-in').flush(ana);
      await harness.fixture.whenStable();

      expect(TestBed.inject(Router).url).toBe('/');
    },
  );
});
