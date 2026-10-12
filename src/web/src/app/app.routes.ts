import { Routes } from '@angular/router';
import { RegisterPage } from './pages/register/register';
import { RunPage } from './pages/run/run';
import { RunsPage } from './pages/runs/runs';
import { SignInPage } from './pages/sign-in/sign-in';
import { SubmitPage } from './pages/submit/submit';
import { redirectSignedIn, requireSession } from './session/require-session.guard';

export const routes: Routes = [
  { path: 'sign-in', component: SignInPage, canActivate: [redirectSignedIn] },
  { path: 'register', component: RegisterPage, canActivate: [redirectSignedIn] },
  { path: '', pathMatch: 'full', component: SubmitPage, canActivate: [requireSession] },
  { path: 'runs', component: RunsPage, canActivate: [requireSession] },
  { path: 'runs/:id', component: RunPage, canActivate: [requireSession] },
  { path: '**', redirectTo: '' },
];
