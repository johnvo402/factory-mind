import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ApiResponse } from '../api/api.models';
import { API_ROUTES } from '../api/api.routes';
import { authInterceptor } from './auth.interceptor';
import { AuthSessionResponse } from './auth.models';
import { AuthService } from './auth.service';

describe('AuthService', () => {
  let auth: AuthService;
  let http: HttpClient;
  let httpTesting: HttpTestingController;

  const session = (accessToken: string): ApiResponse<AuthSessionResponse> => ({
    success: true,
    message: 'OK',
    data: {
      accessToken,
      user: {
        id: 'user-id',
        name: 'FactoryMind Admin',
        email: 'admin@factorymind.local',
        role: 'Admin',
        companyId: 'company-id',
      },
    },
  });

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    auth = TestBed.inject(AuthService);
    http = TestBed.inject(HttpClient);
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpTesting.verify());

  it('keeps the login session in memory and sends credentials for the refresh cookie', () => {
    const storageSpy = spyOn(localStorage, 'setItem');

    auth.login({ email: 'admin@factorymind.local', password: 'Demo@123' }).subscribe();
    const request = httpTesting.expectOne(API_ROUTES.auth.login);
    expect(request.request.withCredentials).toBeTrue();
    request.flush(session('access-token'));

    expect(auth.accessToken()).toBe('access-token');
    expect(auth.user()?.name).toBe('FactoryMind Admin');
    expect(auth.isAuthenticated()).toBeTrue();
    expect(storageSpy).not.toHaveBeenCalled();
  });

  it('restores an in-memory session from the HttpOnly refresh cookie', async () => {
    const restored = auth.restoreSession();
    const request = httpTesting.expectOne(API_ROUTES.auth.refresh);
    expect(request.request.method).toBe('POST');
    expect(request.request.withCredentials).toBeTrue();
    request.flush(session('restored-token'));

    await restored;

    expect(auth.accessToken()).toBe('restored-token');
    expect(auth.isAuthenticated()).toBeTrue();
  });

  it('adds the in-memory bearer token to API requests', () => {
    authenticate('access-token');

    http.get('/api/documents').subscribe();
    const request = httpTesting.expectOne('/api/documents');

    expect(request.request.headers.get('Authorization')).toBe('Bearer access-token');
    expect(request.request.withCredentials).toBeTrue();
    request.flush({});
  });

  it('shares one refresh when concurrent requests receive 401 and retries each request once', () => {
    authenticate('expired-token');

    http.get('/api/documents').subscribe();
    http.get('/api/conversations').subscribe();
    const documents = httpTesting.expectOne('/api/documents');
    const conversations = httpTesting.expectOne('/api/conversations');
    documents.flush({}, { status: 401, statusText: 'Unauthorized' });
    conversations.flush({}, { status: 401, statusText: 'Unauthorized' });

    const refresh = httpTesting.expectOne(API_ROUTES.auth.refresh);
    refresh.flush(session('new-token'));

    const retriedDocuments = httpTesting.expectOne('/api/documents');
    const retriedConversations = httpTesting.expectOne('/api/conversations');
    expect(retriedDocuments.request.headers.get('Authorization')).toBe('Bearer new-token');
    expect(retriedConversations.request.headers.get('Authorization')).toBe('Bearer new-token');
    retriedDocuments.flush({});
    retriedConversations.flush({});
    expect(auth.accessToken()).toBe('new-token');
  });

  function authenticate(accessToken: string): void {
    auth.login({ email: 'admin@factorymind.local', password: 'Demo@123' }).subscribe();
    httpTesting.expectOne(API_ROUTES.auth.login).flush(session(accessToken));
  }
});
