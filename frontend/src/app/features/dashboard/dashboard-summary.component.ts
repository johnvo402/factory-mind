import { Component, inject, OnInit } from '@angular/core';
import { DashboardStore } from './dashboard.store';

@Component({
  selector: 'app-dashboard-summary',
  templateUrl: './dashboard-summary.component.html',
  styleUrl: './dashboard-summary.component.scss',
})
export class DashboardSummaryComponent implements OnInit {
  protected readonly store = inject(DashboardStore);

  ngOnInit(): void {
    void this.store.load();
  }
}
