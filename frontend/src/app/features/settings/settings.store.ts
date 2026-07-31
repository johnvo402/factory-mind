import { inject, Injectable, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { businessDataErrorMessage } from '../data/business-data-error';
import { SettingsApiService } from './settings-api.service';
import {
  AiSettings,
  CompanySettings,
  CreateUserInput,
  UpdateUserInput,
  UserSettings,
} from './settings.models';

@Injectable({ providedIn: 'root' })
export class SettingsStore {
  private readonly api = inject(SettingsApiService);
  private readonly companyState = signal<CompanySettings | null>(null);
  private readonly usersState = signal<UserSettings[]>([]);
  private readonly aiState = signal<AiSettings | null>(null);
  private readonly loadingState = signal(false);
  private readonly errorState = signal('');
  private readonly messageState = signal('');

  readonly company = this.companyState.asReadonly();
  readonly users = this.usersState.asReadonly();
  readonly ai = this.aiState.asReadonly();
  readonly isLoading = this.loadingState.asReadonly();
  readonly error = this.errorState.asReadonly();
  readonly message = this.messageState.asReadonly();

  async load(): Promise<void> {
    await this.run(async () => {
      const [company, users, ai] = await Promise.all([
        firstValueFrom(this.api.getCompany()),
        firstValueFrom(this.api.getUsers()),
        firstValueFrom(this.api.getAi()),
      ]);
      this.companyState.set(company.data);
      this.usersState.set(users.data ?? []);
      this.aiState.set(ai.data);
    });
  }

  async updateCompany(name: string): Promise<boolean> {
    return this.run(async () => {
      const response = await firstValueFrom(this.api.updateCompany(name));
      this.companyState.set(response.data);
      this.messageState.set('Đã cập nhật công ty.');
    });
  }

  async createUser(input: CreateUserInput): Promise<boolean> {
    return this.run(async () => {
      await firstValueFrom(this.api.createUser(input));
      await this.reloadUsers();
      this.messageState.set('Đã tạo người dùng.');
    });
  }

  async updateUser(userId: string, input: UpdateUserInput): Promise<boolean> {
    return this.run(async () => {
      await firstValueFrom(this.api.updateUser(userId, input));
      await this.reloadUsers();
      this.messageState.set('Đã cập nhật người dùng.');
    });
  }

  async reindex(): Promise<boolean> {
    return this.run(async () => {
      const response = await firstValueFrom(this.api.reindexDocuments());
      this.messageState.set(`Đã đưa ${response.data?.queuedCount ?? 0} tài liệu vào hàng đợi re-index.`);
    });
  }

  private async reloadUsers(): Promise<void> {
    const response = await firstValueFrom(this.api.getUsers());
    this.usersState.set(response.data ?? []);
  }

  private async run(action: () => Promise<void>): Promise<boolean> {
    this.loadingState.set(true);
    this.errorState.set('');
    this.messageState.set('');
    try {
      await action();
      return true;
    } catch (error: unknown) {
      this.errorState.set(businessDataErrorMessage(error));
      return false;
    } finally {
      this.loadingState.set(false);
    }
  }
}
