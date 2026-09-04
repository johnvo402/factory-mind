import { Component, input } from '@angular/core';
import { UiIconComponent } from '../../shared/ui/ui-icon.component';
import { ChatMessage } from './chat.models';
import { MarkdownPipe } from './markdown.pipe';

@Component({
  selector: 'app-chat-message',
  imports: [MarkdownPipe, UiIconComponent],
  templateUrl: './chat-message.component.html',
  styleUrl: './chat-message.component.scss',
})
export class ChatMessageComponent {
  readonly message = input.required<ChatMessage>();
  readonly streaming = input(false);

  protected scoreLabel(score: number): string {
    return `Độ khớp ${Math.round(score * 100)}%`;
  }

  protected entityTypeLabel(entityType: string): string {
    const labels: Record<string, string> = {
      machine: 'Máy',
      material: 'Nguyên liệu',
      inventory: 'Tồn kho',
      product: 'Sản phẩm',
      production_order: 'Lệnh sản xuất',
    };
    return labels[entityType] ?? entityType;
  }

  protected evidenceDetail(detail: string): string {
    const parts = detail
      .split(';')
      .map((part) => part.trim())
      .filter(Boolean);

    if (parts.length === 0 || parts.some((part) => !part.includes('='))) {
      return detail;
    }

    return parts
      .map((part) => {
        const separatorIndex = part.indexOf('=');
        const key = part.slice(0, separatorIndex).trim();
        const value = part.slice(separatorIndex + 1).trim();
        return `${this.evidenceFieldLabel(key)}: ${this.evidenceValue(key, value)}`;
      })
      .join(' · ');
  }

  private evidenceFieldLabel(key: string): string {
    const labels: Record<string, string> = {
      status: 'Trạng thái',
      updatedAt: 'Cập nhật',
      unit: 'Đơn vị tính',
      warehouse: 'Kho lưu trữ',
      quantity: 'Số lượng',
    };
    return labels[key] ?? key;
  }

  private evidenceValue(key: string, value: string): string {
    if (key === 'status') {
      const statuses: Record<string, string> = {
        available: 'Sẵn sàng',
        running: 'Đang vận hành',
        maintenance: 'Đang bảo trì',
        offline: 'Ngừng hoạt động',
        planned: 'Đã lên kế hoạch',
        in_progress: 'Đang sản xuất',
        completed: 'Đã hoàn thành',
        cancelled: 'Đã hủy',
      };
      return statuses[value] ?? value;
    }

    if (key === 'updatedAt') {
      const timestamp = new Date(value);
      if (!Number.isNaN(timestamp.getTime())) {
        return new Intl.DateTimeFormat('vi-VN', {
          dateStyle: 'short',
          timeStyle: 'short',
          timeZone: 'UTC',
        }).format(timestamp) + ' UTC';
      }
    }

    return value;
  }
}
