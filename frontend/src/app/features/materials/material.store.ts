import { inject, Injectable, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { businessDataErrorMessage } from '../data/business-data-error';
import { MaterialApiService } from './material-api.service';
import { Material, MaterialInput } from './material.models';

@Injectable({ providedIn: 'root' })
export class MaterialStore {
  private readonly api = inject(MaterialApiService);
  private readonly materialItems = signal<Material[]>([]);
  private readonly loadingState = signal(false);
  private readonly savingState = signal(false);
  private readonly errorState = signal('');
  private readonly searchState = signal('');

  readonly materials = this.materialItems.asReadonly();
  readonly isLoading = this.loadingState.asReadonly();
  readonly isSaving = this.savingState.asReadonly();
  readonly error = this.errorState.asReadonly();
  readonly search = this.searchState.asReadonly();

  async load(search = this.searchState()): Promise<void> {
    this.searchState.set(search.trim());
    this.loadingState.set(true);
    this.errorState.set('');
    try {
      const response = await firstValueFrom(this.api.getMaterials(this.searchState()));
      this.materialItems.set(response.data ?? []);
    } catch (error: unknown) {
      this.errorState.set(businessDataErrorMessage(error));
    } finally {
      this.loadingState.set(false);
    }
  }

  async save(materialId: string | null, input: MaterialInput): Promise<boolean> {
    this.savingState.set(true);
    this.errorState.set('');
    try {
      if (materialId) {
        await firstValueFrom(this.api.updateMaterial(materialId, input));
      } else {
        await firstValueFrom(this.api.createMaterial(input));
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

  async delete(materialId: string): Promise<boolean> {
    this.savingState.set(true);
    this.errorState.set('');
    try {
      await firstValueFrom(this.api.deleteMaterial(materialId));
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
