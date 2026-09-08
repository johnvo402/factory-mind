import { Component, inject, input, output, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { DialogFocusDirective } from '../../shared/ui/dialog-focus.directive';
import { UiIconComponent } from '../../shared/ui/ui-icon.component';
import { ExcelImportEntityType } from './excel-import.models';
import { ExcelImportApiService } from './excel-import-api.service';
import { ExcelImportStore } from './excel-import.store';

@Component({
  selector: 'app-excel-import-wizard',
  imports: [DialogFocusDirective, UiIconComponent],
  providers: [ExcelImportStore],
  templateUrl: './excel-import-wizard.component.html',
  styleUrl: './excel-import-wizard.component.scss',
})
export class ExcelImportWizardComponent {
  private readonly api = inject(ExcelImportApiService);
  readonly entityType = input.required<ExcelImportEntityType>();
  readonly closed = output<void>();
  readonly imported = output<number>();
  protected readonly templateDownloading = signal(false);
  protected readonly templateError = signal('');

  constructor(protected readonly store: ExcelImportStore) {}

  protected entityLabel(): string {
    const labels: Record<ExcelImportEntityType, string> = {
      machine: 'máy móc',
      material: 'nguyên liệu',
      inventory: 'tồn kho',
      product: 'sản phẩm',
      production_order: 'lệnh sản xuất',
    };
    return labels[this.entityType()];
  }

  protected chooseFile(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (file) {
      void this.store.previewFile(this.entityType(), file);
    }
  }

  protected async downloadTemplate(): Promise<void> {
    if (this.templateDownloading()) return;
    this.templateDownloading.set(true);
    this.templateError.set('');
    try {
      const entityType = this.entityType();
      const blob = await firstValueFrom(this.api.downloadTemplate(entityType));
      const objectUrl = URL.createObjectURL(blob);
      try {
        const anchor = document.createElement('a');
        anchor.href = objectUrl;
        anchor.download = `factorymind-${entityType.replace('_', '-')}-import-template.xlsx`;
        document.body.appendChild(anchor);
        anchor.click();
        anchor.remove();
      } finally {
        URL.revokeObjectURL(objectUrl);
      }
    } catch {
      this.templateError.set('Không thể tải file mẫu. Vui lòng thử lại.');
    } finally {
      this.templateDownloading.set(false);
    }
  }

  protected changeMapping(field: string, event: Event): void {
    this.store.setMapping(field, (event.target as HTMLSelectElement).value);
  }

  protected async runImport(): Promise<void> {
    const imported = await this.store.import(this.entityType());
    if (imported) {
      this.imported.emit(this.store.result()?.importedCount ?? 0);
    }
  }
}
