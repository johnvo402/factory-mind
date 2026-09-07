import { Component, inject, input, output, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ChatApiService } from './chat-api.service';
import { AiActionProposal } from './chat.models';

@Component({
  selector: 'app-ai-action-proposal-card',
  templateUrl: './ai-action-proposal-card.component.html',
  styleUrl: './ai-action-proposal-card.component.scss',
})
export class AiActionProposalCardComponent {
  private readonly api = inject(ChatApiService);
  readonly proposal = input.required<AiActionProposal>();
  readonly proposalChange = output<AiActionProposal>();
  protected readonly busy = signal(false);
  protected readonly error = signal('');

  protected get disabled(): boolean {
    return this.busy()
      || this.proposal().status !== 'pending'
      || new Date(this.proposal().expiresAt).getTime() <= Date.now();
  }

  protected async confirm(): Promise<void> {
    if (this.disabled) return;
    this.busy.set(true);
    this.error.set('');
    try {
      const response = await firstValueFrom(this.api.confirmAction(this.proposal().proposalId));
      if (response.success && response.data) this.proposalChange.emit(response.data);
    } catch {
      this.error.set('Không thể xác nhận. Dữ liệu có thể đã thay đổi; hãy tải lại đề xuất.');
    } finally {
      this.busy.set(false);
    }
  }

  protected async cancel(): Promise<void> {
    if (this.disabled) return;
    this.busy.set(true);
    this.error.set('');
    try {
      const response = await firstValueFrom(this.api.cancelAction(this.proposal().proposalId));
      if (response.success && response.data) this.proposalChange.emit(response.data);
    } catch {
      this.error.set('Không thể hủy đề xuất lúc này.');
    } finally {
      this.busy.set(false);
    }
  }
}
