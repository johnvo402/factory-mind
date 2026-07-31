import { Component, inject, input, output } from '@angular/core';
import { ChatStore } from './chat.store';

@Component({
  selector: 'app-chat-sidebar',
  templateUrl: './chat-sidebar.component.html',
  styleUrl: './chat-sidebar.component.scss',
})
export class ChatSidebarComponent {
  protected readonly store = inject(ChatStore);
  readonly userName = input.required<string>();
  readonly logoutRequested = output<void>();
}
