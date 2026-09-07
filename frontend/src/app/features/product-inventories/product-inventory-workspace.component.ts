import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { DialogFocusDirective } from '../../shared/ui/dialog-focus.directive';
import { UiIconComponent } from '../../shared/ui/ui-icon.component';
import { ProductInventoryTransactionType } from './product-inventory.models';
import { ProductInventoryStore } from './product-inventory.store';

@Component({
  selector: 'app-product-inventory-workspace',
  imports: [DatePipe, DecimalPipe, ReactiveFormsModule, DialogFocusDirective, UiIconComponent],
  templateUrl: './product-inventory-workspace.component.html',
  styleUrls: ['../data/entity-workspace.scss', './product-inventory-workspace.component.scss'],
})
export class ProductInventoryWorkspaceComponent implements OnInit {
  protected readonly store = inject(ProductInventoryStore);
  protected readonly historyOpen = signal(false);
  protected readonly balanceForm = new FormGroup({
    search: new FormControl('', { nonNullable: true }),
    warehouseId: new FormControl('', { nonNullable: true }),
    productId: new FormControl('', { nonNullable: true }),
  });
  protected readonly historyForm = new FormGroup({
    warehouseId: new FormControl('', { nonNullable: true }),
    productId: new FormControl('', { nonNullable: true }),
    transactionType: new FormControl<ProductInventoryTransactionType | ''>('', {
      nonNullable: true,
    }),
    from: new FormControl('', { nonNullable: true }),
    to: new FormControl('', { nonNullable: true }),
  });
  protected readonly totalPages = computed(() =>
    Math.max(1, Math.ceil(this.store.transactionCount() / this.store.transactionPageSize())),
  );

  ngOnInit(): void {
    void this.store.initialize();
  }

  protected searchBalances(event: Event): void {
    event.preventDefault();
    const value = this.balanceForm.getRawValue();
    void this.store.loadBalances({
      search: value.search,
      warehouseId: value.warehouseId || undefined,
      productId: value.productId || undefined,
    });
  }

  protected clearBalanceFilters(): void {
    this.balanceForm.reset({ search: '', warehouseId: '', productId: '' });
    void this.store.loadBalances();
  }

  protected async showHistory(): Promise<void> {
    this.historyOpen.set(true);
    await this.loadHistoryPage(1);
  }

  protected applyHistoryFilters(event: Event): void {
    event.preventDefault();
    void this.loadHistoryPage(1);
  }

  protected clearHistoryFilters(): void {
    this.historyForm.reset({
      warehouseId: '',
      productId: '',
      transactionType: '',
      from: '',
      to: '',
    });
    void this.loadHistoryPage(1);
  }

  protected loadHistoryPage(page: number): Promise<boolean> {
    const value = this.historyForm.getRawValue();
    return this.store.loadTransactions({
      warehouseId: value.warehouseId || undefined,
      productId: value.productId || undefined,
      transactionType: value.transactionType || undefined,
      from: value.from ? new Date(`${value.from}T00:00:00`).toISOString() : undefined,
      to: value.to ? new Date(`${value.to}T23:59:59.999`).toISOString() : undefined,
      page,
      pageSize: this.store.transactionPageSize(),
    });
  }

  protected transactionTypeLabel(type: ProductInventoryTransactionType): string {
    const labels: Record<ProductInventoryTransactionType, string> = {
      ProductionOutput: 'Nhập thành phẩm từ sản xuất',
    };
    return labels[type];
  }
}
