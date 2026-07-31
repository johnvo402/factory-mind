import { inject, Injectable, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { businessDataErrorMessage } from '../data/business-data-error';
import { ExcelImportApiService } from './excel-import-api.service';
import {
  ExcelImportEntityType,
  ExcelImportResult,
  ExcelPreview,
} from './excel-import.models';

@Injectable()
export class ExcelImportStore {
  private readonly api = inject(ExcelImportApiService);
  private readonly fileState = signal<File | null>(null);
  private readonly previewState = signal<ExcelPreview | null>(null);
  private readonly mappingState = signal<Record<string, string>>({});
  private readonly resultState = signal<ExcelImportResult | null>(null);
  private readonly loadingState = signal(false);
  private readonly errorState = signal('');

  readonly file = this.fileState.asReadonly();
  readonly preview = this.previewState.asReadonly();
  readonly mapping = this.mappingState.asReadonly();
  readonly result = this.resultState.asReadonly();
  readonly isLoading = this.loadingState.asReadonly();
  readonly error = this.errorState.asReadonly();

  async previewFile(entityType: ExcelImportEntityType, file: File): Promise<void> {
    this.fileState.set(file);
    this.previewState.set(null);
    this.resultState.set(null);
    this.loadingState.set(true);
    this.errorState.set('');
    try {
      const response = await firstValueFrom(this.api.preview(entityType, file));
      this.previewState.set(response.data);
      this.mappingState.set({ ...(response.data?.suggestedMapping ?? {}) });
    } catch (error: unknown) {
      this.errorState.set(businessDataErrorMessage(error));
    } finally {
      this.loadingState.set(false);
    }
  }

  setMapping(field: string, header: string): void {
    this.mappingState.update(mapping => ({ ...mapping, [field]: header }));
  }

  hasCompleteMapping(): boolean {
    const preview = this.previewState();
    return !!preview && preview.requiredFields.every(field => !!this.mappingState()[field]);
  }

  async import(entityType: ExcelImportEntityType): Promise<boolean> {
    const file = this.fileState();
    if (!file || !this.hasCompleteMapping()) {
      return false;
    }

    this.loadingState.set(true);
    this.errorState.set('');
    this.resultState.set(null);
    try {
      const response = await firstValueFrom(this.api.import(entityType, this.mappingState(), file));
      this.resultState.set(response.data);
      return !!response.data && response.data.errors.length === 0;
    } catch (error: unknown) {
      this.errorState.set(businessDataErrorMessage(error));
      return false;
    } finally {
      this.loadingState.set(false);
    }
  }
}
