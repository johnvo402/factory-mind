import { Component, computed, ElementRef, inject, input, output, viewChild } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterOutlet } from '@angular/router';
import { filter, map, startWith } from 'rxjs';
import { WorkspaceSidebarComponent } from './workspace-sidebar.component';
import { WorkspaceView } from './workspace.models';

@Component({
  selector: 'app-workspace',
  imports: [RouterOutlet, WorkspaceSidebarComponent],
  templateUrl: './workspace.component.html',
  styleUrl: './workspace.component.scss',
})
export class WorkspaceComponent {
  private readonly router = inject(Router);
  private readonly mainContent = viewChild<ElementRef<HTMLElement>>('mainContent');
  private readonly routeUrl = toSignal(
    this.router.events.pipe(
      filter((event): event is NavigationEnd => event instanceof NavigationEnd),
      map((event) => event.urlAfterRedirects),
      startWith(this.router.url),
    ),
    { initialValue: this.router.url },
  );

  readonly userName = input.required<string>();
  readonly userRole = input.required<string>();
  readonly logoutRequested = output<void>();
  protected readonly activeView = computed<WorkspaceView>(() => {
    const url = this.routeUrl();
    if (url.startsWith('/knowledge')) return 'knowledge';
    if (url.startsWith('/data')) return 'data';
    if (url.startsWith('/settings')) return 'settings';
    return 'chat';
  });

  protected focusMainContent(): void {
    this.mainContent()?.nativeElement.focus();
  }
}
