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

  protected login(): void {
    if (this.loginForm.invalid) { this.loginForm.markAllAsTouched(); return; }
    this.loading.set(true); this.error.set('');
    this.http.post<ApiResponse<LoginResult>>('http://localhost:5150/api/auth/login', this.loginForm.getRawValue()).subscribe({
      next: response => { this.loading.set(false); if (!response.success || !response.data) { this.error.set(response.message); return; } localStorage.setItem('factorymind.accessToken', response.data.accessToken); this.userName.set(response.data.user.name); this.loggedIn.set(true); },
      error: error => { this.loading.set(false); this.error.set(error.error?.message ?? 'Không thể kết nối đến FactoryMind API.'); }
    });
  }
  protected logout(): void { localStorage.removeItem('factorymind.accessToken'); this.loggedIn.set(false); }
}
