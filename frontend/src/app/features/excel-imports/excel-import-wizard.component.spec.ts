import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Subject, of, throwError } from 'rxjs';
import { ExcelImportApiService } from './excel-import-api.service';
import { ExcelImportWizardComponent } from './excel-import-wizard.component';

describe('ExcelImportWizardComponent template download', () => {
  let fixture: ComponentFixture<ExcelImportWizardComponent>;
  let api: jasmine.SpyObj<ExcelImportApiService>;

  beforeEach(async () => {
    api = jasmine.createSpyObj<ExcelImportApiService>('ExcelImportApiService', [
      'preview',
      'import',
      'downloadTemplate',
    ]);
    await TestBed.configureTestingModule({
      imports: [ExcelImportWizardComponent],
      providers: [{ provide: ExcelImportApiService, useValue: api }],
    }).compileComponents();
    fixture = TestBed.createComponent(ExcelImportWizardComponent);
    fixture.componentRef.setInput('entityType', 'production_order');
    fixture.detectChanges();
  });

  it('renders template guidance before a file is selected', () => {
    expect(fixture.nativeElement.textContent).toContain('Tải file mẫu');
    expect(fixture.nativeElement.textContent).toContain('Data');
    expect(fixture.nativeElement.textContent).toContain('Hướng dẫn');
  });

  it('downloads the template with the current type and deterministic filename', async () => {
    const blob = new Blob(['xlsx'], {
      type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
    });
    api.downloadTemplate.and.returnValue(of(blob));
    spyOn(URL, 'createObjectURL').and.returnValue('blob:template');
    const revoke = spyOn(URL, 'revokeObjectURL');
    let downloadedFileName = '';
    spyOn(HTMLAnchorElement.prototype, 'click').and.callFake(function (
      this: HTMLAnchorElement,
    ) {
      downloadedFileName = this.download;
    });

    templateButton().click();
    await fixture.whenStable();

    expect(api.downloadTemplate).toHaveBeenCalledWith('production_order');
    expect(downloadedFileName).toBe('factorymind-production-order-import-template.xlsx');
    expect(revoke).toHaveBeenCalledWith('blob:template');
  });

  it('disables repeat clicks while the template request is running', async () => {
    const response = new Subject<Blob>();
    api.downloadTemplate.and.returnValue(response);
    spyOn(URL, 'createObjectURL').and.returnValue('blob:template');
    spyOn(URL, 'revokeObjectURL');
    spyOn(HTMLAnchorElement.prototype, 'click');

    templateButton().click();
    fixture.detectChanges();

    expect(templateButton().disabled).toBeTrue();
    expect(fixture.nativeElement.textContent).toContain('Đang tạo file mẫu...');
    response.next(new Blob(['xlsx']));
    response.complete();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(templateButton().disabled).toBeFalse();
  });

  it('shows a controlled error when template download fails', async () => {
    api.downloadTemplate.and.returnValue(throwError(() => new Error('offline')));

    templateButton().click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain(
      'Không thể tải file mẫu. Vui lòng thử lại.',
    );
  });

  function templateButton(): HTMLButtonElement {
    return (Array.from(fixture.nativeElement.querySelectorAll('button')) as HTMLButtonElement[]).find(
      (button) => button.textContent?.includes('file mẫu'),
    )!;
  }
});
