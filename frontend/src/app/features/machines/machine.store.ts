import { inject, Injectable, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { businessDataErrorMessage } from '../data/business-data-error';
import { MachineApiService } from './machine-api.service';
import { Machine, MachineInput } from './machine.models';
import { WorkCenterApiService } from '../work-centers/work-center-api.service';
import { WorkCenter } from '../work-centers/work-center.models';

@Injectable({ providedIn: 'root' })
export class MachineStore {
  private readonly api = inject(MachineApiService);
  private readonly workCenterApi = inject(WorkCenterApiService);
  private readonly machineItems = signal<Machine[]>([]);
  private readonly loadingState = signal(false);
  private readonly savingState = signal(false);
  private readonly errorState = signal('');
  private readonly searchState = signal('');
  private readonly workCenterItems = signal<WorkCenter[]>([]);

  readonly machines = this.machineItems.asReadonly();
  readonly isLoading = this.loadingState.asReadonly();
  readonly isSaving = this.savingState.asReadonly();
  readonly error = this.errorState.asReadonly();
  readonly search = this.searchState.asReadonly();
  readonly workCenters = this.workCenterItems.asReadonly();

  async initialize(): Promise<void> {
    this.loadingState.set(true);
    this.errorState.set('');
    try {
      const [machineResponse, workCenterResponse] = await Promise.all([
        firstValueFrom(this.api.getMachines()),
        firstValueFrom(this.workCenterApi.getWorkCenters()),
      ]);
      this.machineItems.set(machineResponse.data ?? []);
      this.workCenterItems.set(workCenterResponse.data ?? []);
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
      const response = await firstValueFrom(this.api.getMachines(this.searchState()));
      this.machineItems.set(response.data ?? []);
    } catch (error: unknown) {
      this.errorState.set(businessDataErrorMessage(error));
    } finally {
      this.loadingState.set(false);
    }
  }

  async save(machineId: string | null, input: MachineInput): Promise<boolean> {
    this.savingState.set(true);
    this.errorState.set('');
    try {
      if (machineId) {
        await firstValueFrom(this.api.updateMachine(machineId, input));
      } else {
        await firstValueFrom(this.api.createMachine(input));
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

  async delete(machineId: string): Promise<boolean> {
    this.savingState.set(true);
    this.errorState.set('');
    try {
      await firstValueFrom(this.api.deleteMachine(machineId));
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
