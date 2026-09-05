import { Routes } from '@angular/router';
import { adminGuard, managerGuard } from './core/auth/role.guard';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'chat' },
  {
    path: 'chat',
    loadComponent: () => import('./features/chat/chat-workspace.component')
      .then((module) => module.ChatWorkspaceComponent),
  },
  {
    path: 'knowledge',
    loadComponent: () => import('./features/knowledge/knowledge-workspace.component')
      .then((module) => module.KnowledgeWorkspaceComponent),
  },
  { path: 'data', pathMatch: 'full', redirectTo: 'data/machines' },
  {
    path: 'data/:view',
    canActivate: [managerGuard],
    loadComponent: () => import('./features/data/data-workspace.component')
      .then((module) => module.DataWorkspaceComponent),
  },
  {
    path: 'settings',
    canActivate: [adminGuard],
    loadComponent: () => import('./features/settings/settings-workspace.component')
      .then((module) => module.SettingsWorkspaceComponent),
  },
  { path: '**', redirectTo: 'chat' },
];
