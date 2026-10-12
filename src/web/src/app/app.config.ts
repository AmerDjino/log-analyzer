import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { routes } from './app.routes';
import { sessionEndedInterceptor } from './session/session-ended.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    // The fetch backend sends a File body unchanged.
    provideHttpClient(withFetch(), withInterceptors([sessionEndedInterceptor])),
  ],
};
