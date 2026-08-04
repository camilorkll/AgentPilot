import { Routes } from '@angular/router';
import { adminGuard, authGuard } from './core/guards';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'chat' },
  {
    path: 'login',
    loadComponent: () => import('./features/login/login').then((m) => m.Login),
  },
  {
    path: 'chat',
    canActivate: [authGuard],
    loadComponent: () => import('./features/chat/chat').then((m) => m.Chat),
  },
  {
    path: 'campaigns',
    canActivate: [adminGuard],
    loadComponent: () => import('./features/campaigns/campaigns').then((m) => m.Campaigns),
  },
  {
    path: 'documents',
    canActivate: [adminGuard],
    loadComponent: () => import('./features/documents/documents').then((m) => m.Documents),
  },
  {
    path: 'metrics',
    canActivate: [adminGuard],
    loadComponent: () => import('./features/metrics/metrics').then((m) => m.Metrics),
  },
  { path: '**', redirectTo: 'chat' },
];
