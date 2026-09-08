export interface WorkCenter {
  id: string;
  code: string;
  name: string;
  description: string | null;
  parallelCapacity: number;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface WorkCenterShift {
  dayOfWeek: number;
  startTime: string;
  endTime: string;
}

export interface WorkCenterDayOff { date: string; }

export interface WorkCenterCalendar {
  workCenterId: string;
  workCenterCode: string;
  parallelCapacity: number;
  timeZoneId: string;
  shifts: WorkCenterShift[];
  daysOff: WorkCenterDayOff[];
}

export interface WorkCenterCalendarInput {
  parallelCapacity: number;
  shifts: WorkCenterShift[];
  daysOff: WorkCenterDayOff[];
}

export interface WorkCenterInput {
  code: string;
  name: string;
  description: string | null;
}
