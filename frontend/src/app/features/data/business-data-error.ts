import { HttpErrorResponse } from '@angular/common/http';
import { ProblemDetails } from '../../core/api/api.models';

const localizedBusinessErrors: Readonly<Record<string, string>> = {
  'boms.active_not_found': 'Sản phẩm chưa có BOM đang hoạt động.',
  'boms.items_required': 'BOM cần có ít nhất một vật tư trước khi kích hoạt.',
  'routings.active_not_found': 'Sản phẩm chưa có Routing đang hoạt động.',
  'routings.operations_required': 'Routing cần có ít nhất một công đoạn trước khi kích hoạt.',
  'inventories.insufficient_stock': 'Tồn kho khả dụng không đủ để thực hiện thao tác này.',
  'production_orders.locked_bom_required': 'Lệnh sản xuất chưa khóa phiên bản BOM.',
  'production_orders.locked_routing_required': 'Lệnh sản xuất chưa khóa phiên bản Routing.',
  'production_orders.allocations_required': 'Cần phân bổ kho cho từng vật tư trong BOM đã khóa.',
  'production_orders.allocation_quantity_invalid': 'Số lượng phân bổ của mỗi vật tư phải lớn hơn 0.',
  'production_orders.extra_allocation_material': 'Phân bổ chứa vật tư không thuộc BOM đã khóa.',
  'production_orders.missing_allocation_material': 'Còn vật tư trong BOM đã khóa chưa được phân bổ.',
  'production_orders.allocation_total_mismatch': 'Tổng phân bổ phải khớp chính xác nhu cầu vật tư do máy chủ tính.',
  'planning.calendar_invalid': 'Lịch Work Center không hợp lệ. Kiểm tra lại ca làm việc.',
  'planning.timezone_invalid': 'Múi giờ vận hành của doanh nghiệp không hợp lệ.',
  'planning.invalid_horizon': 'Khung thời gian kế hoạch nằm ngoài giới hạn cho phép.',
  'planning.invalid_filter': 'Bộ lọc kế hoạch không hợp lệ.',
};

export function businessDataErrorMessage(error: unknown): string {
  if (error instanceof HttpErrorResponse) {
    const problem = error.error as ProblemDetails | undefined;
    const localized = problem?.code ? localizedBusinessErrors[problem.code] : undefined;
    if (localized) return localized;
    const detail = problem?.detail;
    if (detail) return detail;
    if (error.status === 0) return 'Không thể kết nối tới FactoryMind API.';
    return `Yêu cầu thất bại (${error.status}). Vui lòng thử lại.`;
  }
  return 'Không thể kết nối tới FactoryMind API.';
}
