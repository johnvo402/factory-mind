import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { ReactiveFormsModule, FormControl, FormGroup, Validators } from '@angular/forms';

interface LoginResult { accessToken: string; refreshToken: string; user: { name: string; email: string; role: string; }; }
interface ApiResponse<T> { success: boolean; message: string; data: T | null; }

@Component({ selector: 'app-root', imports: [ReactiveFormsModule], templateUrl: './app.html', styleUrl: './app.scss' })
export class App {
  private readonly http = inject(HttpClient);
  protected readonly loggedIn = signal(false);
  protected readonly loading = signal(false);
  protected readonly error = signal('');
  protected readonly userName = signal('');
  protected readonly loginForm = new FormGroup({
    email: new FormControl('admin@factorymind.local', { nonNullable: true, validators: [Validators.required, Validators.email] }),
    password: new FormControl('Demo@123', { nonNullable: true, validators: [Validators.required] })
  });

  constructor() {
    const accessToken = localStorage.getItem('factorymind.accessToken');
    const user = localStorage.getItem('factorymind.user');
    if (accessToken && user) {
      this.userName.set(JSON.parse(user).name);
      this.loggedIn.set(true);
    }
  }

  protected login(): void {
    if (this.loginForm.invalid) { this.loginForm.markAllAsTouched(); return; }
    this.loading.set(true); this.error.set('');
    this.http.post<ApiResponse<LoginResult>>('http://localhost:5150/api/auth/login', this.loginForm.getRawValue()).subscribe({
      next: response => { this.loading.set(false); if (!response.success || !response.data) { this.error.set(response.message); return; } this.saveSession(response.data); },
      error: error => { this.loading.set(false); this.error.set(error.error?.message ?? 'Không thể kết nối đến FactoryMind API.'); }
    });
  }
  protected logout(): void {
    const refreshToken = localStorage.getItem('factorymind.refreshToken');
    if (refreshToken) this.http.post('http://localhost:5150/api/auth/logout', { refreshToken }).subscribe();
    localStorage.removeItem('factorymind.accessToken');
    localStorage.removeItem('factorymind.refreshToken');
    localStorage.removeItem('factorymind.user');
    this.loggedIn.set(false);
  }

  private saveSession(session: LoginResult): void {
    localStorage.setItem('factorymind.accessToken', session.accessToken);
    localStorage.setItem('factorymind.refreshToken', session.refreshToken);
    localStorage.setItem('factorymind.user', JSON.stringify(session.user));
    this.userName.set(session.user.name);
    this.loggedIn.set(true);
  }
}
