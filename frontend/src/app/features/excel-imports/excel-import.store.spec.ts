import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { ExcelImportApiService } from './excel-import-api.service';
import { ExcelImportStore } from './excel-import.store';

describe('ExcelImportStore', () => {
  let api: jasmine.SpyObj<ExcelImportApiService>;
  let store: ExcelImportStore;
  let file: File;

  beforeEach(() => {
    api = jasmine.createSpyObj<ExcelImportApiService>('ExcelImportApiService', ['preview', 'import']);
    TestBed.configureTestingModule({
      providers: [ExcelImportStore, { provide: ExcelImportApiService, useValue: api }],
    });
    store = TestBed.inject(ExcelImportStore);
    file = new File(['workbook'], 'machines.xlsx');
  });

  it('uses suggested mappings from preview', async () => {
    api.preview.and.returnValue(of({
      success: true,
      message: 'OK',
      data: {
        headers: ['Code', 'Name', 'Status'],
        rows: [{ Code: 'M-001', Name: 'Injection', Status: 'available' }],
        totalRows: 1,
        requiredFields: ['code', 'name', 'status'],
        suggestedMapping: { code: 'Code', name: 'Name', status: 'Status' },
      },
    }));

    await store.previewFile('machine', file);

    expect(store.mapping()).toEqual({ code: 'Code', name: 'Name', status: 'Status' });
    expect(store.hasCompleteMapping()).toBeTrue();
  });

  it('returns false and exposes row errors without treating them as a transport failure', async () => {
    api.preview.and.returnValue(of({
      success: true,
      message: 'OK',
      data: {
        headers: ['Code', 'Name', 'Status'],
        rows: [],
        totalRows: 1,
        requiredFields: ['code', 'name', 'status'],
        suggestedMapping: { code: 'Code', name: 'Name', status: 'Status' },
      },
    }));
    api.import.and.returnValue(of({
      success: true,
      message: 'OK',
      data: { importedCount: 0, errors: [{ row: 2, field: 'code', message: 'Duplicate.' }] },
    }));
    await store.previewFile('machine', file);

    const imported = await store.import('machine');

    expect(imported).toBeFalse();
    expect(store.result()?.errors[0].row).toBe(2);
    expect(store.error()).toBe('');
  });
});
