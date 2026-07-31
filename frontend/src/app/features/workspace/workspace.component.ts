import { Component, input, output, signal } from '@angular/core';
import { ChatWorkspaceComponent } from '../chat/chat-workspace.component';
import { DataWorkspaceComponent } from '../data/data-workspace.component';
import { WorkspaceSidebarComponent } from './workspace-sidebar.component';
import { WorkspaceView } from './workspace.models';

@Component({
  selector: 'app-workspace',
  imports: [ChatWorkspaceComponent, DataWorkspaceComponent, WorkspaceSidebarComponent],
  templateUrl: './workspace.component.html',
  styleUrl: './workspace.component.scss',
})
export class WorkspaceComponent {
  readonly userName = input.required<string>();
  readonly logoutRequested = output<void>();
  protected readonly activeView = signal<WorkspaceView>('chat');

  protected selectView(view: WorkspaceView): void {
    this.activeView.set(view);
  }
}
