import { HttpClient, HttpContext } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { catchError, finalize, firstValueFrom, map, Observable, of, shareReplay, tap, throwError } from 'rxjs';
import { ApiResponse } from '../api/api.models';
import { API_ROUTES } from '../api/api.routes';
import { SKIP_AUTH_INTERCEPTOR } from './auth-http.context';
import { AuthSessionResponse, LoginCredentials, UserProfile } from './auth.models';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly accessTokenState = signal<string | null>(null);
  private readonly userState = signal<UserProfile | null>(null);
  private refreshInFlight$: Observable<string> | null = null;

  readonly accessToken = this.accessTokenState.asReadonly();
  readonly user = this.userState.asReadonly();
  readonly isAuthenticated = computed(
    () => this.accessTokenState() !== null && this.userState() !== null,
  );

  login(credentials: LoginCredentials): Observable<void> {
    return this.http
      .post<ApiResponse<AuthSessionResponse>>(API_ROUTES.auth.login, credentials, {
        context: this.skipAuthContext(),
        withCredentials: true,
      })
      .pipe(
        map((response) => this.requireSession(response)),
        tap((session) => this.setSession(session)),
        map(() => undefined),
      );
  }

  restoreSession(): Promise<void> {
    return firstValueFrom(
      this.refreshAccessToken().pipe(
        map(() => undefined),
        catchError(() => of(undefined)),
      ),
    );
  }

  refreshAccessToken(): Observable<string> {
    if (this.refreshInFlight$) {
      return this.refreshInFlight$;
    }

    this.refreshInFlight$ = this.http
      .post<ApiResponse<AuthSessionResponse>>(
        API_ROUTES.auth.refresh,
        {},
        {
          context: this.skipAuthContext(),
          withCredentials: true,
        },
      )
      .pipe(
        map((response) => this.requireSession(response)),
        tap((session) => this.setSession(session)),
        map((session) => session.accessToken),
        catchError((error) => {
          this.clearSession();
          return throwError(() => error);
        }),
        finalize(() => {
          this.refreshInFlight$ = null;
        }),
        shareReplay({ bufferSize: 1, refCount: false }),
      );

    return this.refreshInFlight$;
  }

  logout(): Observable<void> {
    this.clearSession();
    return this.http
      .post<ApiResponse<object>>(
        API_ROUTES.auth.logout,
        {},
        {
          context: this.skipAuthContext(),
          withCredentials: true,
        },
      )
      .pipe(map(() => undefined));
  }

  private setSession(session: AuthSessionResponse): void {
    this.accessTokenState.set(session.accessToken);
    this.userState.set(session.user);
  }

  private clearSession(): void {
    this.accessTokenState.set(null);
    this.userState.set(null);
  }

  private requireSession(response: ApiResponse<AuthSessionResponse>): AuthSessionResponse {
    if (!response.success || !response.data) {
      throw new Error(response.message || 'Authentication failed.');
    }

    return response.data;
  }

  private skipAuthContext(): HttpContext {
    return new HttpContext().set(SKIP_AUTH_INTERCEPTOR, true);
  }
}
