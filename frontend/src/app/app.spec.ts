import { computed, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { of } from 'rxjs';
import { App } from './app';
import { AuthService } from './core/auth/auth.service';
import { UserProfile } from './core/auth/auth.models';

describe('App', () => {
  const router = jasmine.createSpyObj<Router>('Router', ['navigateByUrl']);
  const user = signal<UserProfile | null>(null);
  const auth = {
    user: user.asReadonly(),
    isAuthenticated: computed(() => user() !== null),
    login: jasmine.createSpy('login').and.returnValue(of(undefined)),
    logout: jasmine.createSpy('logout').and.returnValue(of(undefined)),
  };

  beforeEach(async () => {
    user.set(null);
    auth.login.calls.reset();
    auth.logout.calls.reset();
    router.navigateByUrl.calls.reset();
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [
        { provide: AuthService, useValue: auth },
        { provide: Router, useValue: router },
      ],
    }).compileComponents();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance;
    expect(app).toBeTruthy();
  });

  it('should render the login screen without a restored session', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('h1')?.textContent).toContain('Chào mừng trở lại');
  });

  it('returns to Chat after login so guards cannot reuse a stale protected route', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    const email = compiled.querySelector<HTMLInputElement>('#login-email')!;
    const password = compiled.querySelector<HTMLInputElement>('#login-password')!;

    email.value = 'operator@factorymind.local';
    email.dispatchEvent(new Event('input'));
    password.value = 'Demo@123';
    password.dispatchEvent(new Event('input'));
    compiled.querySelector<HTMLFormElement>('form')!.dispatchEvent(new Event('submit'));

    expect(auth.login).toHaveBeenCalled();
    expect(router.navigateByUrl).toHaveBeenCalledWith('/chat');
  });
});
