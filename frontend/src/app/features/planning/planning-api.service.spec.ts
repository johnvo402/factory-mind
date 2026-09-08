import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { PlanningApiService } from './planning-api.service';

describe('PlanningApiService', () => {
  it('uses the bounded schedule-preview route and horizon query', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(PlanningApiService);
    const http = TestBed.inject(HttpTestingController);

    service.getPreview(14).subscribe();

    const request = http.expectOne('/api/production-orders/schedule-preview?horizonDays=14');
    expect(request.request.method).toBe('GET');
    request.flush({ success: true, message: 'OK', data: null });
    http.verify();
  });
});
