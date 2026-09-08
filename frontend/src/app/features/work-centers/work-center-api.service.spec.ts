import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { WorkCenterApiService } from './work-center-api.service';
import { WorkCenterCalendarInput } from './work-center.models';

describe('WorkCenterApiService calendar', () => {
  beforeEach(() => TestBed.configureTestingModule({
    providers: [provideHttpClient(), provideHttpClientTesting()],
  }));

  it('loads calendar from the Work Center route', () => {
    const service = TestBed.inject(WorkCenterApiService);
    const http = TestBed.inject(HttpTestingController);
    service.getCalendar('wc-1').subscribe();
    const request = http.expectOne('/api/work-centers/wc-1/calendar');
    expect(request.request.method).toBe('GET');
    request.flush({ success: true, message: 'OK', data: null });
  });

  it('saves the exact capacity, shifts, and days-off payload', () => {
    const service = TestBed.inject(WorkCenterApiService);
    const http = TestBed.inject(HttpTestingController);
    const input: WorkCenterCalendarInput = {
      parallelCapacity: 2,
      shifts: [{ dayOfWeek: 1, startTime: '08:00:00', endTime: '17:00:00' }],
      daysOff: [{ date: '2026-09-15' }],
    };
    service.replaceCalendar('wc-1', input).subscribe();
    const request = http.expectOne('/api/work-centers/wc-1/calendar');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual(input);
    request.flush({ success: true, message: 'OK', data: null });
  });
});
