export type MachineStatus = 'available' | 'running' | 'maintenance' | 'offline';

export interface Machine {
  id: string;
  code: string;
  name: string;
  status: MachineStatus;
  createdAt: string;
  updatedAt: string;
}

export interface MachineInput {
  code: string;
  name: string;
  status: MachineStatus;
}
