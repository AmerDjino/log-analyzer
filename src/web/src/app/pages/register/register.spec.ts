import { TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';
import { Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { Account } from '../../api/api-client';
import { ana, limits, problems } from '../../testing/answers';
import { answerSession, flushError, provideTestApp, typeInto } from '../../testing/test-app';

describe('RegisterPage', () => {
  let http: HttpTestingController;
  let harness: RouterTestingHarness;

  beforeEach(async () => {
    TestBed.configureTestingModule({ providers: provideTestApp() });
    http = TestBed.inject(HttpTestingController);
    harness = await RouterTestingHarness.create();
  });

  afterEach(() => http.verify());

  async function open(account: Account | null): Promise<HTMLElement> {
    const navigation = harness.navigateByUrl('/register');
    await answerSession(http, account);
    await navigation;
    return harness.routeNativeElement!;
  }

  async function createAccount(page: HTMLElement, email: string, password: string): Promise<void> {
    typeInto(page, '#email', email);
    typeInto(page, '#password', password);
    page.querySelector('form')!.dispatchEvent(new Event('submit'));
    await harness.fixture.whenStable();
  }

  // The message that the field's aria-describedby points to.
  function messageAt(page: HTMLElement, field: string): string {
    const id = page.querySelector(`#${field}`)!.getAttribute('aria-describedby');
    return id === null ? '' : (page.querySelector(`#${id}`)?.textContent ?? '');
  }

  it('[analysis-ui/new-account-is-signed-in] registers, signs in with the same credentials and opens the submit page', async () => {
    const page = await open(null);

    await createAccount(page, 'ana@example.com', 'correct horse battery');
    const register = http.expectOne('/api/auth/register');
    expect(register.request.body).toEqual({
      email: 'ana@example.com',
      password: 'correct horse battery',
    });
    register.flush(ana, { status: 201, statusText: 'Created' });
    const signIn = await vi.waitFor(() => http.expectOne('/api/auth/sign-in'));
    expect(signIn.request.body).toEqual({
      email: 'ana@example.com',
      password: 'correct horse battery',
    });
    signIn.flush(ana);
    await harness.fixture.whenStable();
    http.expectOne('/api/runs/limits').flush(limits);

    expect(TestBed.inject(Router).url).toBe('/');
    expect(harness.routeNativeElement!.querySelector('h2')?.textContent).toContain('Submit a log');
  });

  it('[analysis-ui/short-password-is-explained-at-its-field] shows the password-length message at the password field, whose `aria-describedby` points to it, and keeps the email address', async () => {
    const page = await open(null);

    await createAccount(page, 'ana@example.com', 'elevenchars');
    flushError(http.expectOne('/api/auth/register'), problems.passwordTooShort);
    await harness.fixture.whenStable();

    expect(messageAt(page, 'password')).toContain('at least 12 characters');
    expect(page.querySelector<HTMLInputElement>('#email')!.value).toBe('ana@example.com');
  });

  it('[analysis-ui/taken-email-address-is-explained-at-its-field] shows the taken-address message at the email field, whose `aria-describedby` points to it, and keeps the email address', async () => {
    const page = await open(null);

    await createAccount(page, 'ana@example.com', 'correct horse battery');
    flushError(http.expectOne('/api/auth/register'), problems.emailTaken);
    await harness.fixture.whenStable();

    expect(messageAt(page, 'email')).toContain('An account with this email address already exists');
    expect(page.querySelector<HTMLInputElement>('#email')!.value).toBe('ana@example.com');
  });

  it('sends a signed-in person who opens /register to /', async () => {
    await open(ana);
    http.expectOne('/api/runs/limits').flush(limits);

    expect(TestBed.inject(Router).url).toBe('/');
  });
});
