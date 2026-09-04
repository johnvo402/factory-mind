import { Component, inject, input, output } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { UiIconComponent } from '../../shared/ui/ui-icon.component';
import { ChatStore } from '../chat/chat.store';
import { WorkspaceView } from './workspace.models';

@Component({
  selector: 'app-workspace-sidebar',
  imports: [RouterLink, UiIconComponent],
  templateUrl: './workspace-sidebar.component.html',
  styleUrl: './workspace-sidebar.component.scss',
})
export class WorkspaceSidebarComponent {
  protected readonly store = inject(ChatStore);
  private readonly router = inject(Router);
  readonly userName = input.required<string>();
  readonly userRole = input.required<string>();
  readonly activeView = input.required<WorkspaceView>();
  readonly logoutRequested = output<void>();

  protected startNewConversation(): void {
    void this.router.navigateByUrl('/chat');
    this.store.startNewConversation();
  }

  protected selectConversation(conversationId: string): void {
    void this.router.navigateByUrl('/chat');
    this.store.selectConversation(conversationId);
  }
}
