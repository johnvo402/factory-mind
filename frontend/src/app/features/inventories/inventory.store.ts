import { inject, Injectable, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { businessDataErrorMessage } from '../data/business-data-error';
import { MaterialApiService } from '../materials/material-api.service';
import { Material } from '../materials/material.models';
import { InventoryApiService } from './inventory-api.service';
import { Inventory, InventoryInput } from './inventory.models';

@Injectable({ providedIn: 'root' })
export class InventoryStore {
  private readonly api = inject(InventoryApiService);
  private readonly materialApi = inject(MaterialApiService);
  private readonly inventoryItems = signal<Inventory[]>([]);
  private readonly materialItems = signal<Material[]>([]);
  private readonly loadingState = signal(false);
  private readonly savingState = signal(false);
  private readonly errorState = signal('');
  private readonly searchState = signal('');

  readonly inventories = this.inventoryItems.asReadonly();
  readonly materials = this.materialItems.asReadonly();
  readonly isLoading = this.loadingState.asReadonly();
  readonly isSaving = this.savingState.asReadonly();
  readonly error = this.errorState.asReadonly();
  readonly search = this.searchState.asReadonly();

  async initialize(): Promise<void> {
    this.loadingState.set(true);
    this.errorState.set('');
    try {
      const [inventoryResponse, materialResponse] = await Promise.all([
        firstValueFrom(this.api.getInventories()),
        firstValueFrom(this.materialApi.getMaterials()),
      ]);
      this.inventoryItems.set(inventoryResponse.data ?? []);
      this.materialItems.set(materialResponse.data ?? []);
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

  async save(inventoryId: string | null, input: InventoryInput): Promise<boolean> {
    this.savingState.set(true);
    this.errorState.set('');
    try {
      if (inventoryId) {
        await firstValueFrom(this.api.updateInventory(inventoryId, input));
      } else {
        await firstValueFrom(this.api.createInventory(input));
      }
      await this.load();
      return true;
    } catch (error: unknown) {
      this.errorState.set(businessDataErrorMessage(error));
      return false;
    } finally {
      this.savingState.set(false);
    }
  }

  async delete(inventoryId: string): Promise<boolean> {
    this.savingState.set(true);
    this.errorState.set('');
    try {
      await firstValueFrom(this.api.deleteInventory(inventoryId));
      await this.load();
      return true;
    } catch (error: unknown) {
      this.errorState.set(businessDataErrorMessage(error));
      return false;
    } finally {
      this.savingState.set(false);
    }
  }

  clearError(): void {
    this.errorState.set('');
  }
}
