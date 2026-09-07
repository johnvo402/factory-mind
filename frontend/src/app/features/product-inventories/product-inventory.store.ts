import { inject, Injectable, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { businessDataErrorMessage } from '../data/business-data-error';
import { InventoryApiService } from '../inventories/inventory-api.service';
import { Warehouse } from '../inventories/inventory.models';
import { ProductApiService } from '../products/product-api.service';
import { Product } from '../products/product.models';
import { ProductInventoryApiService } from './product-inventory-api.service';
import {
  ProductInventoryBalance,
  ProductInventoryBalanceFilters,
  ProductInventoryTransaction,
  ProductInventoryTransactionFilters,
} from './product-inventory.models';

@Injectable({ providedIn: 'root' })
export class ProductInventoryStore {
  private readonly api = inject(ProductInventoryApiService);
  private readonly productApi = inject(ProductApiService);
  private readonly inventoryApi = inject(InventoryApiService);
  private readonly balanceItems = signal<ProductInventoryBalance[]>([]);
  private readonly transactionItems = signal<ProductInventoryTransaction[]>([]);
  private readonly productItems = signal<Product[]>([]);
  private readonly warehouseItems = signal<Warehouse[]>([]);
  private readonly transactionPageState = signal(1);
  private readonly transactionPageSizeState = signal(25);
  private readonly transactionCountState = signal(0);
  private readonly loadingState = signal(false);
  private readonly historyLoadingState = signal(false);
  private readonly errorState = signal('');

  readonly balances = this.balanceItems.asReadonly();
  readonly transactions = this.transactionItems.asReadonly();
  readonly products = this.productItems.asReadonly();
  readonly warehouses = this.warehouseItems.asReadonly();
  readonly transactionPage = this.transactionPageState.asReadonly();
  readonly transactionPageSize = this.transactionPageSizeState.asReadonly();
  readonly transactionCount = this.transactionCountState.asReadonly();
  readonly isLoading = this.loadingState.asReadonly();
  readonly isLoadingHistory = this.historyLoadingState.asReadonly();
  readonly error = this.errorState.asReadonly();

  async initialize(): Promise<void> {
    this.loadingState.set(true);
    this.errorState.set('');
    try {
      const [balanceResponse, productResponse, warehouseResponse] = await Promise.all([
        firstValueFrom(this.api.getBalances()),
        firstValueFrom(this.productApi.getProducts()),
        firstValueFrom(this.inventoryApi.getWarehouses()),
      ]);
      this.balanceItems.set(balanceResponse.data ?? []);
      this.productItems.set(productResponse.data ?? []);
      this.warehouseItems.set(warehouseResponse.data ?? []);
    } catch (error: unknown) {
      this.errorState.set(businessDataErrorMessage(error));
    } finally {
      this.loadingState.set(false);
    }
  }

  async loadBalances(filters: ProductInventoryBalanceFilters = {}): Promise<void> {
    this.loadingState.set(true);
    this.errorState.set('');
    try {
      const response = await firstValueFrom(this.api.getBalances(filters));
      this.balanceItems.set(response.data ?? []);
    } catch (error: unknown) {
      this.errorState.set(businessDataErrorMessage(error));
    } finally {
      this.loadingState.set(false);
    }
  }

  async loadTransactions(filters: ProductInventoryTransactionFilters): Promise<boolean> {
    this.historyLoadingState.set(true);
    this.errorState.set('');
    try {
      const response = await firstValueFrom(this.api.getTransactions(filters));
      this.transactionItems.set(response.data?.items ?? []);
      this.transactionPageState.set(response.data?.page ?? filters.page);
      this.transactionPageSizeState.set(response.data?.pageSize ?? filters.pageSize);
      this.transactionCountState.set(response.data?.totalCount ?? 0);
      return true;
    } catch (error: unknown) {
      this.errorState.set(businessDataErrorMessage(error));
      return false;
    } finally {
      this.historyLoadingState.set(false);
    }
  }
}
