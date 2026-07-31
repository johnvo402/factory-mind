import { Component, inject, input, output } from '@angular/core';
import { ChatStore } from '../chat/chat.store';
import { WorkspaceView } from './workspace.models';

@Component({
  selector: 'app-workspace-sidebar',
  templateUrl: './workspace-sidebar.component.html',
  styleUrl: './workspace-sidebar.component.scss',
})
export class WorkspaceSidebarComponent {
  protected readonly store = inject(ChatStore);
  readonly userName = input.required<string>();
  readonly userRole = input.required<string>();
  readonly activeView = input.required<WorkspaceView>();
  readonly viewRequested = output<WorkspaceView>();
  readonly logoutRequested = output<void>();

  protected startNewConversation(): void {
    this.viewRequested.emit('chat');
    this.store.startNewConversation();
  }

  protected selectConversation(conversationId: string): void {
    this.viewRequested.emit('chat');
    this.store.selectConversation(conversationId);
  }
}
