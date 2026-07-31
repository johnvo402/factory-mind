import { inject, Injectable, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { businessDataErrorMessage } from '../data/business-data-error';
import { KnowledgeApiService } from './knowledge-api.service';
import { KnowledgeDocument, KnowledgeSearchResult } from './knowledge.models';

@Injectable({ providedIn: 'root' })
export class KnowledgeStore {
  private readonly api = inject(KnowledgeApiService);
  private readonly documentsState = signal<KnowledgeDocument[]>([]);
  private readonly resultsState = signal<KnowledgeSearchResult[]>([]);
  private readonly loadingState = signal(false);
  private readonly errorState = signal('');
  private readonly messageState = signal('');

  readonly documents = this.documentsState.asReadonly();
  readonly results = this.resultsState.asReadonly();
  readonly isLoading = this.loadingState.asReadonly();
  readonly error = this.errorState.asReadonly();
  readonly message = this.messageState.asReadonly();

  async load(silent = false): Promise<void> {
    if (!silent) {
      this.loadingState.set(true);
      this.errorState.set('');
    }
    try {
      const response = await firstValueFrom(this.api.getDocuments());
      this.documentsState.set(response.data ?? []);
    } catch (error: unknown) {
      if (!silent) {
        this.errorState.set(businessDataErrorMessage(error));
      }
    } finally {
      if (!silent) {
        this.loadingState.set(false);
      }
    }
  }

  async upload(file: File, title: string): Promise<boolean> {
    return this.run(async () => {
      await firstValueFrom(this.api.upload(file, title));
      await this.load(true);
      this.messageState.set('Tài liệu đã được tải lên và đưa vào hàng đợi xử lý.');
    });
  }

  async process(documentId: string): Promise<boolean> {
    return this.run(async () => {
      await firstValueFrom(this.api.process(documentId));
      await this.load(true);
      this.messageState.set('Đã đưa tài liệu vào hàng đợi xử lý.');
    });
  }

  async search(query: string): Promise<void> {
    await this.run(async () => {
      const response = await firstValueFrom(this.api.search(query.trim()));
      this.resultsState.set(response.data ?? []);
    });
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
