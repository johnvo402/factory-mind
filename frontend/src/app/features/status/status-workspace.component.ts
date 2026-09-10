import { Component, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { UiIconComponent } from '../../shared/ui/ui-icon.component';

@Component({
  selector: 'app-status-workspace',
  imports: [RouterLink, UiIconComponent],
  templateUrl: './status-workspace.component.html',
  styleUrl: './status-workspace.component.scss',
})
export class StatusWorkspaceComponent {
  private readonly route = inject(ActivatedRoute);
  protected readonly code = this.route.snapshot.data['code'] as string;
  protected readonly eyebrow = this.route.snapshot.data['eyebrow'] as string;
  protected readonly title = this.route.snapshot.data['title'] as string;
  protected readonly description = this.route.snapshot.data['description'] as string;
}
