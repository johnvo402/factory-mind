import { inject, Injectable, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { businessDataErrorMessage } from '../data/business-data-error';
import { BomApiService } from '../boms/bom-api.service';
import { MaterialRequirements } from '../boms/bom.models';
import { ProductApiService } from '../products/product-api.service';
import { Product } from '../products/product.models';
import { ProductionOrderApiService } from './production-order-api.service';
import { ProductionOrder, ProductionOrderInput } from './production-order.models';

@Injectable({ providedIn: 'root' })
export class ProductionOrderStore {
  private readonly api = inject(ProductionOrderApiService);
  private readonly productApi = inject(ProductApiService);
  private readonly bomApi = inject(BomApiService);
  private readonly orderItems = signal<ProductionOrder[]>([]);
  private readonly productItems = signal<Product[]>([]);
  private readonly loadingState = signal(false);
  private readonly savingState = signal(false);
  private readonly errorState = signal('');
  private readonly searchState = signal('');
  private readonly requirementState = signal<MaterialRequirements | null>(null);
  private readonly requirementLoadingState = signal(false);
  private readonly requirementErrorState = signal('');

  readonly orders = this.orderItems.asReadonly();
  readonly products = this.productItems.asReadonly();
  readonly isLoading = this.loadingState.asReadonly();
  readonly isSaving = this.savingState.asReadonly();
  readonly error = this.errorState.asReadonly();
  readonly search = this.searchState.asReadonly();
  readonly requirements = this.requirementState.asReadonly();
  readonly isLoadingRequirements = this.requirementLoadingState.asReadonly();
  readonly requirementError = this.requirementErrorState.asReadonly();

  async initialize(): Promise<void> {
    this.loadingState.set(true);
    this.errorState.set('');
    try {
      const [orderResponse, productResponse] = await Promise.all([
        firstValueFrom(this.api.getProductionOrders()),
        firstValueFrom(this.productApi.getProducts()),
      ]);
      this.orderItems.set(orderResponse.data ?? []);
      this.productItems.set(productResponse.data ?? []);
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
      const response = await firstValueFrom(this.api.getProductionOrders(this.searchState()));
      this.orderItems.set(response.data ?? []);
    } catch (error: unknown) {
      this.errorState.set(businessDataErrorMessage(error));
    } finally {
      this.loadingState.set(false);
    }
  }

  async save(orderId: string | null, input: ProductionOrderInput): Promise<boolean> {
    this.savingState.set(true);
    this.errorState.set('');
    try {
      if (orderId) {
        await firstValueFrom(this.api.updateProductionOrder(orderId, input));
      } else {
        await firstValueFrom(this.api.createProductionOrder(input));
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

  async delete(orderId: string): Promise<boolean> {
    this.savingState.set(true);
    this.errorState.set('');
    try {
      await firstValueFrom(this.api.deleteProductionOrder(orderId));
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

  async checkMaterials(orderId: string): Promise<void> {
    this.requirementLoadingState.set(true);
    this.requirementErrorState.set('');
    this.requirementState.set(null);
    try {
      const response = await firstValueFrom(this.bomApi.getProductionOrderRequirements(orderId));
      this.requirementState.set(response.data ?? null);
    } catch (error: unknown) {
      this.requirementErrorState.set(businessDataErrorMessage(error));
    } finally {
      this.requirementLoadingState.set(false);
    }
  }

  clearRequirements(): void {
    this.requirementState.set(null);
    this.requirementErrorState.set('');
    this.requirementLoadingState.set(false);
  }
}
