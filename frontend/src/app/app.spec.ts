import { computed, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { App } from './app';
import { AuthService } from './core/auth/auth.service';
import { UserProfile } from './core/auth/auth.models';

describe('App', () => {
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
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [{ provide: AuthService, useValue: auth }],
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
});
