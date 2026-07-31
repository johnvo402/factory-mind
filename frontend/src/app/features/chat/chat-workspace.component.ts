import {
  Component,
  effect,
  ElementRef,
  inject,
  input,
  OnDestroy,
  OnInit,
  viewChild,
} from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { ChatMessageComponent } from './chat-message.component';
import { ChatStore } from './chat.store';

@Component({
  selector: 'app-chat-workspace',
  imports: [ReactiveFormsModule, ChatMessageComponent],
  templateUrl: './chat-workspace.component.html',
  styleUrl: './chat-workspace.component.scss',
})
export class ChatWorkspaceComponent implements OnInit, OnDestroy {
  protected readonly store = inject(ChatStore);
  private readonly messageViewport =
    viewChild<ElementRef<HTMLDivElement>>('messageViewport');
  readonly userName = input.required<string>();
  protected readonly composer = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, Validators.maxLength(8_000)],
  });
  protected readonly suggestions = [
    'Tóm tắt những thông tin quan trọng trong tài liệu nhà máy.',
    'Máy nào đang sẵn sàng vận hành theo tài liệu hiện có?',
    'Liệt kê các lưu ý an toàn và trích dẫn nguồn.',
  ];

  constructor() {
    effect(() => {
      const messages = this.store.messages();
      if (messages.length === 0) {
        return;
      }

      requestAnimationFrame(() => {
        const viewport = this.messageViewport()?.nativeElement;
        if (viewport) {
          viewport.scrollTop = viewport.scrollHeight;
        }
      });
    });
  }

  ngOnInit(): void {
    void this.store.initialize();
  }

  ngOnDestroy(): void {
    this.store.reset();
  }

  protected async sendMessage(): Promise<void> {
    if (this.composer.invalid || this.store.isStreaming()) {
      this.composer.markAsTouched();
      return;
    }

    const content = this.composer.getRawValue();
    this.composer.setValue('');
    await this.store.sendMessage(content);

    if (this.store.error() && !this.composer.value) {
      this.composer.setValue(content);
    }
  }

  protected sendSuggestion(suggestion: string): void {
    this.composer.setValue(suggestion);
    void this.sendMessage();
  }

  protected handleComposerKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      void this.sendMessage();
    }
  }
}
