import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import { AUTH_REQUEST_RETRIED, SKIP_AUTH_INTERCEPTOR } from './auth-http.context';
import { AuthService } from './auth.service';

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  if (request.context.get(SKIP_AUTH_INTERCEPTOR) || !request.url.startsWith('/api/')) {
    return next(request);
  }

  const auth = inject(AuthService);
  const accessToken = auth.accessToken();
  const authenticatedRequest = request.clone({
    withCredentials: true,
    setHeaders: accessToken ? { Authorization: `Bearer ${accessToken}` } : {},
  });

  return next(authenticatedRequest).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status !== 401 || request.context.get(AUTH_REQUEST_RETRIED)) {
        return throwError(() => error);
      }

      return auth.refreshAccessToken().pipe(
        switchMap((newAccessToken) =>
          next(
            request.clone({
              context: request.context.set(AUTH_REQUEST_RETRIED, true),
              withCredentials: true,
              setHeaders: { Authorization: `Bearer ${newAccessToken}` },
            }),
          ),
        ),
      );
    }),
  );
};
