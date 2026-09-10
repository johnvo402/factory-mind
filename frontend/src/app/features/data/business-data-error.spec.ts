import { HttpErrorResponse } from '@angular/common/http';
import { businessDataErrorMessage } from './business-data-error';

describe('businessDataErrorMessage', () => {
  it('localizes a known business error by its stable code', () => {
    const error = new HttpErrorResponse({
      status: 409,
      error: {
        code: 'boms.active_not_found',
        detail: 'The product does not have an active bill of materials.',
      },
    });

    expect(businessDataErrorMessage(error)).toBe('Sản phẩm chưa có BOM đang hoạt động.');
  });

  it('preserves a specific backend detail for unknown codes', () => {
    const error = new HttpErrorResponse({
      status: 409,
      error: { code: 'custom.error', detail: 'Chi tiết nghiệp vụ từ máy chủ.' },
    });

    expect(businessDataErrorMessage(error)).toBe('Chi tiết nghiệp vụ từ máy chủ.');
  });

  it('uses a recoverable message for a network failure', () => {
    expect(businessDataErrorMessage(new HttpErrorResponse({ status: 0 })))
      .toBe('Không thể kết nối tới FactoryMind API.');
  });
});
