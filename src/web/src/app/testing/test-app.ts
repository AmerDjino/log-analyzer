import { EnvironmentProviders, Provider } from '@angular/core';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { routes } from '../app.routes';

/**
 * The providers of app.config.ts for unit tests: the app's routes and its HTTP setup, with every request answered
 * through HttpTestingController instead of the network.
 */
export function provideTestApp(): (Provider | EnvironmentProviders)[] {
  return [
    provideRouter(routes),
    provideHttpClient(withInterceptors([])),
    provideHttpClientTesting(),
  ];
}
