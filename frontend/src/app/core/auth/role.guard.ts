import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

function hasRole(roles: readonly string[]): boolean | ReturnType<Router['createUrlTree']> {
  const auth = inject(AuthService);
  const router = inject(Router);
  const role = auth.user()?.role;
  return role && roles.includes(role) ? true : router.createUrlTree(['/chat']);
}

export const managerGuard: CanActivateFn = () => hasRole(['Admin', 'Manager']);
export const adminGuard: CanActivateFn = () => hasRole(['Admin']);
