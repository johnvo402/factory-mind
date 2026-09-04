import { Component, input, output } from '@angular/core';
import { DialogFocusDirective } from '../../shared/ui/dialog-focus.directive';
import { UiIconComponent } from '../../shared/ui/ui-icon.component';
import { ExcelImportEntityType } from './excel-import.models';
import { ExcelImportStore } from './excel-import.store';

@Component({
  selector: 'app-excel-import-wizard',
  imports: [DialogFocusDirective, UiIconComponent],
  providers: [ExcelImportStore],
  templateUrl: './excel-import-wizard.component.html',
  styleUrl: './excel-import-wizard.component.scss',
})
export class ExcelImportWizardComponent {
  readonly entityType = input.required<ExcelImportEntityType>();
  readonly closed = output<void>();
  readonly imported = output<number>();

  constructor(protected readonly store: ExcelImportStore) {}

  protected chooseFile(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (file) {
      void this.store.previewFile(this.entityType(), file);
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
