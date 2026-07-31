import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { Observable, of, throwError } from 'rxjs';
import { ApiResponse } from '../../core/api/api.models';
import { MachineApiService } from './machine-api.service';
import { Machine, MachineInput } from './machine.models';
import { MachineStore } from './machine.store';

describe('MachineStore', () => {
  let store: MachineStore;
  let api: jasmine.SpyObj<MachineApiService>;

  beforeEach(() => {
    api = jasmine.createSpyObj<MachineApiService>('MachineApiService', [
      'getMachines',
      'createMachine',
      'updateMachine',
      'deleteMachine',
    ]);
    TestBed.configureTestingModule({
      providers: [MachineStore, { provide: MachineApiService, useValue: api }],
    });
    store = TestBed.inject(MachineStore);
  });

  it('loads company machines using a trimmed search', async () => {
    api.getMachines.and.returnValue(success([machine()]));

    await store.load('  injection  ');

    expect(api.getMachines).toHaveBeenCalledWith('injection');
    expect(store.search()).toBe('injection');
    expect(store.machines()).toEqual([machine()]);
  });

  it('creates a machine and reloads the canonical list', async () => {
    const input: MachineInput = {
      code: 'M-002',
      name: 'Packing line',
      status: 'available',
    };
    api.createMachine.and.returnValue(success(machine('M-002')));
    api.getMachines.and.returnValue(success([machine('M-002')]));

    const saved = await store.save(null, input);

    expect(saved).toBeTrue();
    expect(api.createMachine).toHaveBeenCalledWith(input);
    expect(store.machines()[0].code).toBe('M-002');
  });

  it('shows Problem Details when deletion fails', async () => {
    api.deleteMachine.and.returnValue(throwError(() => new HttpErrorResponse({
      status: 409,
      error: { detail: 'Machine is in use.' },
    })));

    const deleted = await store.delete('machine-1');

    expect(deleted).toBeFalse();
    expect(store.error()).toBe('Machine is in use.');
  });

  function machine(code = 'M-001'): Machine {
    return {
      id: 'machine-1',
      code,
      name: 'Injection molding',
      status: 'available',
      createdAt: '2026-08-01T00:00:00Z',
      updatedAt: '2026-08-01T00:00:00Z',
    };
  }

  function success<T>(data: T): Observable<ApiResponse<T>> {
    return of({ success: true, message: 'OK', data });
  }
});
