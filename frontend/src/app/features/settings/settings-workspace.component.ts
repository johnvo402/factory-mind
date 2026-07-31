import { Component, inject, OnInit, signal } from '@angular/core';
import { SettingsStore } from './settings.store';
import { UserSettings } from './settings.models';

type SettingsTab = 'company' | 'users' | 'ai';

@Component({
  selector: 'app-settings-workspace',
  templateUrl: './settings-workspace.component.html',
  styleUrl: './settings-workspace.component.scss',
})
export class SettingsWorkspaceComponent implements OnInit {
  protected readonly store = inject(SettingsStore);
  protected readonly activeTab = signal<SettingsTab>('company');
  protected readonly companyName = signal('');
  protected readonly selectedUserId = signal<string | null>(null);
  protected readonly userName = signal('');
  protected readonly userEmail = signal('');
  protected readonly userPassword = signal('');
  protected readonly userRole = signal<UserSettings['role']>('User');
  protected readonly userActive = signal(true);

  async ngOnInit(): Promise<void> {
    await this.store.load();
    this.companyName.set(this.store.company()?.name ?? '');
  }

  protected setTab(tab: SettingsTab): void {
    this.activeTab.set(tab);
  }

  protected async saveCompany(): Promise<void> {
    if (this.companyName().trim()) {
      await this.store.updateCompany(this.companyName());
    }
  }

  protected selectUser(user: UserSettings): void {
    this.selectedUserId.set(user.id);
    this.userName.set(user.name);
    this.userEmail.set(user.email);
    this.userPassword.set('');
    this.userRole.set(user.role);
    this.userActive.set(user.isActive);
  }

  protected newUser(): void {
    this.selectedUserId.set(null);
    this.userName.set('');
    this.userEmail.set('');
    this.userPassword.set('');
    this.userRole.set('User');
    this.userActive.set(true);
  }

  protected async saveUser(): Promise<void> {
    const userId = this.selectedUserId();
    const saved = userId
      ? await this.store.updateUser(userId, {
          name: this.userName(),
          email: this.userEmail(),
          role: this.userRole(),
          isActive: this.userActive(),
        })
      : await this.store.createUser({
          name: this.userName(),
          email: this.userEmail(),
          password: this.userPassword(),
          role: this.userRole(),
        });
    if (saved) {
      this.newUser();
    }
  }

  protected changeRole(event: Event): void {
    this.userRole.set((event.target as HTMLSelectElement).value as UserSettings['role']);
  }
}
