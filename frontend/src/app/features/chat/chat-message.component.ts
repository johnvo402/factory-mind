import { Component, input } from '@angular/core';
import { ChatMessage } from './chat.models';
import { MarkdownPipe } from './markdown.pipe';

@Component({
  selector: 'app-chat-message',
  imports: [MarkdownPipe],
  templateUrl: './chat-message.component.html',
  styleUrl: './chat-message.component.scss',
})
export class ChatMessageComponent {
  readonly message = input.required<ChatMessage>();
  readonly streaming = input(false);

  protected scoreLabel(score: number): string {
    return `${Math.round(score * 100)}% match`;
  }
}
