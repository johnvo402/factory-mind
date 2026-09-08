import { inject, Injectable, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { businessDataErrorMessage } from '../data/business-data-error';
import { BomApiService } from '../boms/bom-api.service';
import { MaterialRequirements } from '../boms/bom.models';
import { ProductApiService } from '../products/product-api.service';
import { Product } from '../products/product.models';
import { MachineApiService } from '../machines/machine-api.service';
import { Machine } from '../machines/machine.models';
import { InventoryApiService } from '../inventories/inventory-api.service';
import { Warehouse } from '../inventories/inventory.models';
import { ProductionOrderApiService } from './production-order-api.service';
import {
  CompleteProductionOrderInput,
  ProductionOrder,
  ProductionOrderInput,
  ProductionOrderOperation,
  StartProductionOrderInput,
} from './production-order.models';

@Injectable({ providedIn: 'root' })
export class ProductionOrderStore {
  private readonly api = inject(ProductionOrderApiService);
  private readonly productApi = inject(ProductApiService);
  private readonly bomApi = inject(BomApiService);
  private readonly machineApi = inject(MachineApiService);
  private readonly inventoryApi = inject(InventoryApiService);
  private readonly orderItems = signal<ProductionOrder[]>([]);
  private readonly productItems = signal<Product[]>([]);
  private readonly machineItems = signal<Machine[]>([]);
  private readonly warehouseItems = signal<Warehouse[]>([]);
  private readonly operationItems = signal<ProductionOrderOperation[]>([]);
  private readonly loadingState = signal(false);
  private readonly savingState = signal(false);
  private readonly errorState = signal('');
  private readonly refreshWarningState = signal('');
  private readonly searchState = signal('');
  private readonly requirementState = signal<MaterialRequirements | null>(null);
  private readonly requirementLoadingState = signal(false);
  private readonly requirementErrorState = signal('');
  private readonly operationLoadingState = signal(false);
  private readonly operationErrorState = signal('');

  readonly orders = this.orderItems.asReadonly();
  readonly products = this.productItems.asReadonly();
  readonly machines = this.machineItems.asReadonly();
  readonly warehouses = this.warehouseItems.asReadonly();
  readonly operations = this.operationItems.asReadonly();
  readonly isLoading = this.loadingState.asReadonly();
  readonly isSaving = this.savingState.asReadonly();
  readonly error = this.errorState.asReadonly();
  readonly refreshWarning = this.refreshWarningState.asReadonly();
  readonly search = this.searchState.asReadonly();
  readonly requirements = this.requirementState.asReadonly();
  readonly isLoadingRequirements = this.requirementLoadingState.asReadonly();
  readonly requirementError = this.requirementErrorState.asReadonly();
  readonly isLoadingOperations = this.operationLoadingState.asReadonly();
  readonly operationError = this.operationErrorState.asReadonly();

  async initialize(): Promise<void> {
    this.loadingState.set(true);
    this.errorState.set('');
    this.refreshWarningState.set('');
    try {
      const [orderResponse, productResponse, machineResponse, warehouseResponse] =
        await Promise.all([
          firstValueFrom(this.api.getProductionOrders()),
          firstValueFrom(this.productApi.getProducts()),
          firstValueFrom(this.machineApi.getMachines()),
          firstValueFrom(this.inventoryApi.getWarehouses()),
        ]);
      this.orderItems.set(orderResponse.data ?? []);
      this.productItems.set(productResponse.data ?? []);
      this.machineItems.set(machineResponse.data ?? []);
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
    this.refreshWarningState.set('');
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
    return this.runLifecycle(async () => {
      if (orderId) {
        await firstValueFrom(this.api.updateProductionOrder(orderId, input));
      } else {
        await firstValueFrom(this.api.createProductionOrder(input));
      }
    });
  }

  async delete(orderId: string): Promise<boolean> {
    return this.runLifecycle(() => firstValueFrom(this.api.deleteProductionOrder(orderId)));
  }

  async release(orderId: string): Promise<boolean> {
    const succeeded = await this.runLifecycle(() =>
      firstValueFrom(this.api.releaseProductionOrder(orderId)),
    );
    if (succeeded) this.clearRequirements();
    return succeeded;
  }

  async start(orderId: string, input: StartProductionOrderInput): Promise<boolean> {
    const succeeded = await this.runLifecycle(
      () => firstValueFrom(this.api.startProductionOrder(orderId, input)),
      [() => this.refreshExecution(orderId)],
    );
    if (succeeded) {
      this.clearRequirements();
    }
    return succeeded;
  }

  async complete(orderId: string, input: CompleteProductionOrderInput): Promise<boolean> {
    return this.runLifecycle(
      () => firstValueFrom(this.api.completeProductionOrder(orderId, input)),
      [() => this.loadOperations(orderId)],
    );
  }

  async cancel(orderId: string): Promise<boolean> {
    const succeeded = await this.runLifecycle(() =>
      firstValueFrom(this.api.cancelProductionOrder(orderId)),
    );
    if (succeeded) {
      this.clearRequirements();
      this.operationItems.set([]);
    }
    return succeeded;
  }

  async startOperation(orderId: string, operationId: string, machineId: string): Promise<boolean> {
    return this.transitionOperation(orderId, () =>
      this.api.startOperation(orderId, operationId, machineId),
    );
  }

  async completeOperation(orderId: string, operationId: string): Promise<boolean> {
    return this.transitionOperation(orderId, () =>
      this.api.completeOperation(orderId, operationId),
    );
  }

  async loadOperations(orderId: string): Promise<boolean> {
    this.operationLoadingState.set(true);
    this.operationErrorState.set('');
    this.operationItems.set([]);
    try {
      const response = await firstValueFrom(this.api.getOperations(orderId));
      this.operationItems.set(response.data ?? []);
      return true;
    } catch (error: unknown) {
      this.operationErrorState.set(businessDataErrorMessage(error));
      return false;
    } finally {
      this.operationLoadingState.set(false);
    }
  }

  async loadExecution(orderId: string): Promise<void> {
    await Promise.all([this.loadOperations(orderId), this.loadMachines()]);
  }

  async prepareStart(orderId: string): Promise<boolean> {
    this.requirementLoadingState.set(true);
    this.requirementErrorState.set('');
    this.requirementState.set(null);
    this.errorState.set('');
    try {
      const [requirementResponse, warehouseResponse] = await Promise.all([
        firstValueFrom(this.bomApi.getProductionOrderRequirements(orderId)),
        firstValueFrom(this.inventoryApi.getWarehouses()),
      ]);
      this.requirementState.set(requirementResponse.data ?? null);
      this.warehouseItems.set(warehouseResponse.data ?? []);
      return Boolean(requirementResponse.data);
    } catch (error: unknown) {
      const message = businessDataErrorMessage(error);
      this.requirementErrorState.set(message);
      this.errorState.set(message);
      return false;
    } finally {
      this.requirementLoadingState.set(false);
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

  clearOperations(): void {
    this.operationItems.set([]);
    this.operationErrorState.set('');
    this.operationLoadingState.set(false);
  }

  private async runLifecycle(
    request: () => Promise<unknown>,
    followUpRefreshes: Array<() => Promise<unknown>> = [],
  ): Promise<boolean> {
    this.savingState.set(true);
    this.errorState.set('');
    this.refreshWarningState.set('');
    try {
      await request();
    } catch (error: unknown) {
      this.errorState.set(businessDataErrorMessage(error));
      this.savingState.set(false);
      return false;
    }

    try {
      await this.loadOrdersWithoutSpinner();
    } catch {
      this.setRefreshWarning();
    }
    for (const refresh of followUpRefreshes) {
      try {
        if ((await refresh()) === false) this.setRefreshWarning();
      } catch {
        this.setRefreshWarning();
      }
    }
    this.savingState.set(false);
    return true;
  }

  private async transitionOperation(
    orderId: string,
    request: () => ReturnType<ProductionOrderApiService['startOperation']>,
  ): Promise<boolean> {
    this.savingState.set(true);
    this.errorState.set('');
    this.refreshWarningState.set('');
    try {
      await firstValueFrom(request());
    } catch (error: unknown) {
      this.errorState.set(businessDataErrorMessage(error));
      this.savingState.set(false);
      return false;
    }

    try {
      const [orderResponse, machineResponse, operationResponse] = await Promise.all([
        firstValueFrom(this.api.getProductionOrders(this.searchState())),
        firstValueFrom(this.machineApi.getMachines()),
        firstValueFrom(this.api.getOperations(orderId)),
      ]);
      this.orderItems.set(orderResponse.data ?? []);
      this.machineItems.set(machineResponse.data ?? []);
      this.operationItems.set(operationResponse.data ?? []);
    } catch {
      this.setRefreshWarning();
    } finally {
      this.savingState.set(false);
    }
    return true;
  }

  private async loadOrdersWithoutSpinner(): Promise<void> {
    const response = await firstValueFrom(this.api.getProductionOrders(this.searchState()));
    this.orderItems.set(response.data ?? []);
  }

  private async loadMachines(): Promise<void> {
    try {
      const response = await firstValueFrom(this.machineApi.getMachines());
      this.machineItems.set(response.data ?? []);
    } catch (error: unknown) {
      this.operationErrorState.set(businessDataErrorMessage(error));
    }
  }

  private async refreshExecution(orderId: string): Promise<void> {
    const [machineResponse, operationResponse] = await Promise.all([
      firstValueFrom(this.machineApi.getMachines()),
      firstValueFrom(this.api.getOperations(orderId)),
    ]);
    this.machineItems.set(machineResponse.data ?? []);
    this.operationItems.set(operationResponse.data ?? []);
  }

  private setRefreshWarning(): void {
    this.refreshWarningState.set(
      'Thao tác đã được thực hiện thành công nhưng chưa thể tải lại dữ liệu mới nhất. Vui lòng tải lại màn hình.',
    );
  }
}
