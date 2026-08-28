import { inject, Injectable, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { businessDataErrorMessage } from '../data/business-data-error';
import { MaterialApiService } from '../materials/material-api.service';
import { Material } from '../materials/material.models';
import { BomApiService } from './bom-api.service';
import { Bom, BomInput, MaterialRequirements } from './bom.models';

@Injectable({ providedIn: 'root' })
export class BomStore {
  private readonly api = inject(BomApiService);
  private readonly materialApi = inject(MaterialApiService);
  private readonly bomItems = signal<Bom[]>([]);
  private readonly materialItems = signal<Material[]>([]);
  private readonly loadingState = signal(false);
  private readonly savingState = signal(false);
  private readonly calculatingState = signal(false);
  private readonly errorState = signal('');
  private readonly requirementState = signal<MaterialRequirements | null>(null);

  readonly boms = this.bomItems.asReadonly();
  readonly materials = this.materialItems.asReadonly();
  readonly isLoading = this.loadingState.asReadonly();
  readonly isSaving = this.savingState.asReadonly();
  readonly isCalculating = this.calculatingState.asReadonly();
  readonly error = this.errorState.asReadonly();
  readonly requirements = this.requirementState.asReadonly();

  async load(productId: string): Promise<void> {
    this.loadingState.set(true);
    this.errorState.set('');
    this.requirementState.set(null);
    try {
      const [bomResponse, materialResponse] = await Promise.all([
        firstValueFrom(this.api.getBoms(productId)),
        firstValueFrom(this.materialApi.getMaterials()),
      ]);
      this.bomItems.set(bomResponse.data ?? []);
      this.materialItems.set(materialResponse.data ?? []);
    } catch (error: unknown) {
      this.errorState.set(businessDataErrorMessage(error));
    } finally {
      this.loadingState.set(false);
    }
  }

  async save(productId: string, bomId: string | null, input: BomInput): Promise<Bom | null> {
    this.savingState.set(true);
    this.errorState.set('');
    try {
      const response = await firstValueFrom(
        bomId ? this.api.updateBom(productId, bomId, input) : this.api.createBom(productId, input),
      );
      await this.reloadBoms(productId);
      return response.data ?? null;
    } catch (error: unknown) {
      this.errorState.set(businessDataErrorMessage(error));
      return null;
    } finally {
      this.savingState.set(false);
    }
  }

  async activate(productId: string, bomId: string): Promise<Bom | null> {
    return this.changeStatus(productId, bomId, 'activate');
  }

  async archive(productId: string, bomId: string): Promise<Bom | null> {
    return this.changeStatus(productId, bomId, 'archive');
  }

  async calculate(productId: string, quantity: number): Promise<void> {
    this.calculatingState.set(true);
    this.errorState.set('');
    this.requirementState.set(null);
    try {
      const response = await firstValueFrom(this.api.getProductRequirements(productId, quantity));
      this.requirementState.set(response.data ?? null);
    } catch (error: unknown) {
      this.errorState.set(businessDataErrorMessage(error));
    } finally {
      this.calculatingState.set(false);
    }
  }

  clear(): void {
    this.bomItems.set([]);
    this.materialItems.set([]);
    this.requirementState.set(null);
    this.errorState.set('');
  }

  clearError(): void {
    this.errorState.set('');
  }

  private async reloadBoms(productId: string): Promise<void> {
    const response = await firstValueFrom(this.api.getBoms(productId));
    this.bomItems.set(response.data ?? []);
    this.requirementState.set(null);
  }

  private async changeStatus(
    productId: string,
    bomId: string,
    action: 'activate' | 'archive',
  ): Promise<Bom | null> {
    this.savingState.set(true);
    this.errorState.set('');
    try {
      const response = await firstValueFrom(
        action === 'activate'
          ? this.api.activateBom(productId, bomId)
          : this.api.archiveBom(productId, bomId),
      );
      await this.reloadBoms(productId);
      return response.data ?? null;
    } catch (error: unknown) {
      this.errorState.set(businessDataErrorMessage(error));
      return null;
    } finally {
      this.savingState.set(false);
    }
  }
}
