import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, isDevMode, signal } from '@angular/core';
import { ReactiveFormsModule, FormControl, FormGroup, Validators } from '@angular/forms';
import { ProblemDetails } from './core/api/api.models';
import { AuthService } from './core/auth/auth.service';
import { WorkspaceComponent } from './features/workspace/workspace.component';
import { UiIconComponent } from './shared/ui/ui-icon.component';

@Component({
  selector: 'app-root',
  imports: [ReactiveFormsModule, UiIconComponent, WorkspaceComponent],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  private readonly auth = inject(AuthService);

  protected readonly loggedIn = this.auth.isAuthenticated;
  protected readonly userName = computed(() => this.auth.user()?.name ?? '');
  protected readonly userRole = computed(() => this.auth.user()?.role ?? '');
  protected readonly loading = signal(false);
  protected readonly error = signal('');
  protected readonly passwordVisible = signal(false);
  protected readonly isDevelopment = isDevMode();
  protected readonly loginForm = new FormGroup({
    email: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.email],
    }),
    password: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required],
    }),
  });

  protected login(): void {
    if (this.loginForm.invalid) {
      this.loginForm.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.error.set('');
    this.auth.login(this.loginForm.getRawValue()).subscribe({
      next: () => this.loading.set(false),
      error: (error: HttpErrorResponse) => {
        this.loading.set(false);
        this.error.set(this.errorMessage(error));
      },
    });
  }

  protected logout(): void {
    this.loading.set(true);
    this.error.set('');
    this.auth.logout().subscribe({
      next: () => this.loading.set(false),
      error: (error: HttpErrorResponse) => {
        this.loading.set(false);
        this.error.set(this.errorMessage(error));
      },
    });
  }

  private errorMessage(error: HttpErrorResponse): string {
    return (error.error as ProblemDetails | undefined)?.detail
      ?? 'Unable to connect to the FactoryMind API.';
  }
}
