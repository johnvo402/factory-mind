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
    path: 'planning',
    canActivate: [managerGuard],
    loadComponent: () => import('./features/planning/planning-workspace.component')
      .then((module) => module.PlanningWorkspaceComponent),
  },
  {
    path: 'settings',
    canActivate: [adminGuard],
    loadComponent: () => import('./features/settings/settings-workspace.component')
      .then((module) => module.SettingsWorkspaceComponent),
  },
  {
    path: 'unauthorized',
    data: {
      code: '403',
      eyebrow: 'QUYỀN TRUY CẬP',
      title: 'Bạn chưa có quyền mở khu vực này',
      description: 'Tài khoản hiện tại không có vai trò phù hợp. Hãy quay lại không gian được cấp quyền hoặc liên hệ quản trị viên.',
    },
    loadComponent: () => import('./features/status/status-workspace.component')
      .then((module) => module.StatusWorkspaceComponent),
  },
  {
    path: 'not-found',
    data: {
      code: '404',
      eyebrow: 'KHÔNG TÌM THẤY',
      title: 'Trang bạn tìm không tồn tại',
      description: 'Đường dẫn có thể đã thay đổi hoặc không còn hợp lệ. Bạn có thể trở về Chat để tiếp tục làm việc.',
    },
    loadComponent: () => import('./features/status/status-workspace.component')
      .then((module) => module.StatusWorkspaceComponent),
  },
  { path: '**', redirectTo: 'not-found' },
];
