import { TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';
import { Router } from '@angular/router';
import { App } from './app';
import { Account } from './api/api-client';
import { ana, limits, runListPage } from './testing/answers';
import { answerSession, provideTestApp } from './testing/test-app';

describe('App', () => {
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: provideTestApp(),
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  // The header belongs to App, so these tests render App and navigate with the router, not with RouterTestingHarness.
  async function openAt(url: string, account: Account | null) {
    const fixture = TestBed.createComponent(App);
    const navigation = TestBed.inject(Router).navigateByUrl(url);
    await answerSession(http, account);
    await navigation;
    await fixture.whenStable();
    return fixture;
  }

  function button(element: HTMLElement, text: string): HTMLButtonElement | undefined {
    return Array.from(element.querySelectorAll('button')).find(
      (b) => b.textContent?.trim() === text,
    );
  }

  it('should create the app', () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance;
    expect(app).toBeTruthy();
  });

  it('should render the product name', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('h1')?.textContent).toContain('Log Analyzer');
  });

  it('[analysis-ui/sign-out-returns-to-sign-in] signs out through the API to the sign-in page, and sends a later visit to the run list to sign-in', async () => {
    const fixture = await openAt('/runs', ana);
    http.expectOne('/api/runs').flush(runListPage);
    const element = fixture.nativeElement as HTMLElement;
    const router = TestBed.inject(Router);

    button(element, 'Sign out')!.click();
    const signOut = http.expectOne('/api/auth/sign-out');
    expect(signOut.request.method).toBe('POST');
    signOut.flush(null, { status: 204, statusText: 'No Content' });
    await fixture.whenStable();

    expect(router.url).toBe('/sign-in');
    expect(element.querySelector('h2')?.textContent).toContain('Sign in');

    await router.navigateByUrl('/runs');
    await fixture.whenStable();
    expect(router.url).toMatch(/^\/sign-in\?/);
    expect(element.querySelector('h2')?.textContent).toContain('Sign in');
  });

  it('shows the links, the email address and sign-out only when signed in', async () => {
    const signedOut = await openAt('/sign-in', null);
    const outElement = signedOut.nativeElement as HTMLElement;
    expect(outElement.querySelector('nav')).toBeNull();
    expect(button(outElement, 'Sign out')).toBeUndefined();
    signedOut.destroy();

    TestBed.resetTestingModule();
    await TestBed.configureTestingModule({
      imports: [App],
      providers: provideTestApp(),
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
    const signedIn = await openAt('/runs', ana);
    http.expectOne('/api/runs').flush(runListPage);
    await signedIn.whenStable();
    const inElement = signedIn.nativeElement as HTMLElement;
    const links = Array.from(inElement.querySelectorAll('nav a')).map((a) =>
      a.getAttribute('href'),
    );
    expect(links).toEqual(['/', '/runs']);
    expect(inElement.querySelector('header')?.textContent).toContain('ana@example.com');
    expect(button(inElement, 'Sign out')).toBeDefined();
  });

  it('lands a signed-in person who opens an unknown path on /', async () => {
    await openAt('/nope', ana);
    http.expectOne('/api/runs/limits').flush(limits);

    expect(TestBed.inject(Router).url).toBe('/');
  });
});
