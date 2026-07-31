import { inject, Injectable, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { businessDataErrorMessage } from '../data/business-data-error';
import { ProductApiService } from './product-api.service';
import { Product, ProductInput } from './product.models';

@Injectable({ providedIn: 'root' })
export class ProductStore {
  private readonly api = inject(ProductApiService);
  private readonly productItems = signal<Product[]>([]);
  private readonly loadingState = signal(false);
  private readonly savingState = signal(false);
  private readonly errorState = signal('');
  private readonly searchState = signal('');

  readonly products = this.productItems.asReadonly();
  readonly isLoading = this.loadingState.asReadonly();
  readonly isSaving = this.savingState.asReadonly();
  readonly error = this.errorState.asReadonly();
  readonly search = this.searchState.asReadonly();

  async load(search = this.searchState()): Promise<void> {
    this.searchState.set(search.trim());
    this.loadingState.set(true);
    this.errorState.set('');
    try {
      const response = await firstValueFrom(this.api.getProducts(this.searchState()));
      this.productItems.set(response.data ?? []);
    } catch (error: unknown) {
      this.errorState.set(businessDataErrorMessage(error));
    } finally {
      this.loadingState.set(false);
    }
  }

  async save(productId: string | null, input: ProductInput): Promise<boolean> {
    this.savingState.set(true);
    this.errorState.set('');
    try {
      if (productId) {
        await firstValueFrom(this.api.updateProduct(productId, input));
      } else {
        await firstValueFrom(this.api.createProduct(input));
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

  async delete(productId: string): Promise<boolean> {
    this.savingState.set(true);
    this.errorState.set('');
    try {
      await firstValueFrom(this.api.deleteProduct(productId));
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
