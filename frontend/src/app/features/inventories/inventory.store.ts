import { inject, Injectable, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { businessDataErrorMessage } from '../data/business-data-error';
import { MaterialApiService } from '../materials/material-api.service';
import { Material } from '../materials/material.models';
import { InventoryApiService } from './inventory-api.service';
import {
  Inventory,
  InventoryAdjustmentInput,
  InventoryMovementInput,
  InventoryTransaction,
  InventoryTransferInput,
  Warehouse,
  WarehouseCreateInput,
  WarehouseUpdateInput,
} from './inventory.models';

@Injectable({ providedIn: 'root' })
export class InventoryStore {
  private readonly api = inject(InventoryApiService);
  private readonly materialApi = inject(MaterialApiService);
  private readonly inventoryItems = signal<Inventory[]>([]);
  private readonly materialItems = signal<Material[]>([]);
  private readonly warehouseItems = signal<Warehouse[]>([]);
  private readonly transactionItems = signal<InventoryTransaction[]>([]);
  private readonly transactionCountState = signal(0);
  private readonly loadingState = signal(false);
  private readonly savingState = signal(false);
  private readonly errorState = signal('');
  private readonly searchState = signal('');

  readonly inventories = this.inventoryItems.asReadonly();
  readonly materials = this.materialItems.asReadonly();
  readonly warehouses = this.warehouseItems.asReadonly();
  readonly transactions = this.transactionItems.asReadonly();
  readonly transactionCount = this.transactionCountState.asReadonly();
  readonly isLoading = this.loadingState.asReadonly();
  readonly isSaving = this.savingState.asReadonly();
  readonly error = this.errorState.asReadonly();
  readonly search = this.searchState.asReadonly();

  async initialize(): Promise<void> {
    this.loadingState.set(true);
    this.errorState.set('');
    try {
      const [inventoryResponse, materialResponse, warehouseResponse] = await Promise.all([
        firstValueFrom(this.api.getInventories()),
        firstValueFrom(this.materialApi.getMaterials()),
        firstValueFrom(this.api.getWarehouses()),
      ]);
      this.inventoryItems.set(inventoryResponse.data ?? []);
      this.materialItems.set(materialResponse.data ?? []);
      this.warehouseItems.set(warehouseResponse.data ?? []);
    } catch (error: unknown) {
      this.errorState.set(businessDataErrorMessage(error));
    } finally {
      this.loadingState.set(false);
    }
  }

  async load(search = this.searchState()): Promise<void> {
    this.searchState.set(search.trim());
    this.loadingState.set(true);
    this.errorState.set('');
    try {
      const response = await firstValueFrom(this.api.getInventories(this.searchState()));
      this.inventoryItems.set(response.data ?? []);
    } catch (error: unknown) {
      this.errorState.set(businessDataErrorMessage(error));
    } finally {
      this.loadingState.set(false);
    }
  }

  async loadHistory(): Promise<boolean> {
    this.loadingState.set(true);
    this.errorState.set('');
    try {
      const response = await firstValueFrom(this.api.getTransactions());
      this.transactionItems.set(response.data?.items ?? []);
      this.transactionCountState.set(response.data?.totalCount ?? 0);
      return true;
    } catch (error: unknown) {
      this.errorState.set(businessDataErrorMessage(error));
      return false;
    } finally {
      this.loadingState.set(false);
    }
  }

  async receive(input: InventoryMovementInput): Promise<boolean> {
    return this.runMovement(() => firstValueFrom(this.api.receive(input)));
  }

  async issue(input: InventoryMovementInput): Promise<boolean> {
    return this.runMovement(() => firstValueFrom(this.api.issue(input)));
  }

  async adjust(input: InventoryAdjustmentInput): Promise<boolean> {
    return this.runMovement(() => firstValueFrom(this.api.adjust(input)));
  }

  async transfer(input: InventoryTransferInput): Promise<boolean> {
    return this.runMovement(() => firstValueFrom(this.api.transfer(input)));
  }

  async saveWarehouse(
    warehouseId: string | null,
    input: WarehouseCreateInput | WarehouseUpdateInput,
  ): Promise<boolean> {
    this.savingState.set(true);
    this.errorState.set('');
    try {
      if (warehouseId) {
        await firstValueFrom(
          this.api.updateWarehouse(warehouseId, input as WarehouseUpdateInput),
        );
      } else {
        await firstValueFrom(this.api.createWarehouse(input));
      }
      await this.reloadWarehouses();
      return true;
    } catch (error: unknown) {
      this.errorState.set(businessDataErrorMessage(error));
      return false;
    } finally {
      this.savingState.set(false);
    }
  }

  async deactivateWarehouse(warehouseId: string): Promise<void> {
    this.savingState.set(true);
    this.errorState.set('');
    try {
      await firstValueFrom(this.api.deactivateWarehouse(warehouseId));
      await this.reloadWarehouses();
    } catch (error: unknown) {
      this.errorState.set(businessDataErrorMessage(error));
    } finally {
      this.savingState.set(false);
    }
  }

  clearError(): void {
    this.errorState.set('');
  }

  private async runMovement(request: () => Promise<unknown>): Promise<boolean> {
    this.savingState.set(true);
    this.errorState.set('');
    try {
      await request();
      await this.load();
      return true;
    } catch (error: unknown) {
      this.errorState.set(businessDataErrorMessage(error));
      return false;
    } finally {
      this.savingState.set(false);
    }
  }

  private async reloadWarehouses(): Promise<void> {
    const response = await firstValueFrom(this.api.getWarehouses());
    this.warehouseItems.set(response.data ?? []);
  }
}
