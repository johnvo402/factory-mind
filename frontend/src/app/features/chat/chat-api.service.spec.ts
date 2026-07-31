import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { lastValueFrom, of, toArray } from 'rxjs';
import { BROWSER_FETCH, BrowserFetch } from '../../core/api/browser-fetch.token';
import { AuthService } from '../../core/auth/auth.service';
import { ChatApiService } from './chat-api.service';

describe('ChatApiService', () => {
  let service: ChatApiService;
  let accessToken: string;
  let browserFetch: jasmine.Spy<BrowserFetch>;
  let refreshAccessToken: jasmine.Spy;

  beforeEach(() => {
    accessToken = 'access-token';
    browserFetch = jasmine.createSpy('browserFetch');
    refreshAccessToken = jasmine.createSpy('refreshAccessToken').and.callFake(() => {
      accessToken = 'refreshed-token';
      return of(accessToken);
    });

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: BROWSER_FETCH, useValue: browserFetch },
        {
          provide: AuthService,
          useValue: {
            accessToken: () => accessToken,
            refreshAccessToken,
          },
        },
      ],
    });
    service = TestBed.inject(ChatApiService);
  });

  it('parses the authenticated POST stream into semantic chat events', async () => {
    browserFetch.and.resolveTo(sseResponse([
      'event: conversation\ndata: {"conversationId":"conversation-1"}\n\n',
      'event: token\ndata: {"content":"Answer [S1]."}\n\n',
      'event: citations\ndata: {"citations":[]}\n\n',
      'event: done\ndata: {}\n\n',
    ]));

    const events = await lastValueFrom(
      service.streamMessage('conversation-1', 'Question').pipe(toArray()),
    );

    expect(events).toEqual([
      { type: 'conversation', conversationId: 'conversation-1' },
      { type: 'token', content: 'Answer [S1].' },
      { type: 'citations', citations: [] },
      { type: 'done' },
    ]);
    const [url, init] = browserFetch.calls.mostRecent().args;
    expect(url).toBe('/api/conversations/conversation-1/messages/stream');
    expect(init?.method).toBe('POST');
    expect(new Headers(init?.headers).get('Authorization')).toBe('Bearer access-token');
    expect(init?.credentials).toBe('include');
  });

  it('refreshes once after 401 and retries the stream with the new access token', async () => {
    browserFetch.and.returnValues(
      Promise.resolve(new Response(null, { status: 401, statusText: 'Unauthorized' })),
      Promise.resolve(sseResponse(['event: done\ndata: {}\n\n'])),
    );

    await lastValueFrom(service.streamMessage('conversation-1', 'Question').pipe(toArray()));

    expect(refreshAccessToken).toHaveBeenCalledTimes(1);
    expect(browserFetch).toHaveBeenCalledTimes(2);
    const [, retryInit] = browserFetch.calls.mostRecent().args;
    expect(new Headers(retryInit?.headers).get('Authorization')).toBe('Bearer refreshed-token');
  });

  it('rejects a stream that ends without the done event', async () => {
    browserFetch.and.resolveTo(sseResponse([
      'event: token\ndata: {"content":"Partial"}\n\n',
    ]));

    await expectAsync(lastValueFrom(
      service.streamMessage('conversation-1', 'Question').pipe(toArray()),
    )).toBeRejectedWithError('The AI response stream ended before completion.');
  });

  function sseResponse(chunks: string[]): Response {
    const encoder = new TextEncoder();
    const body = new ReadableStream<Uint8Array>({
      start(controller) {
        for (const chunk of chunks) {
          controller.enqueue(encoder.encode(chunk));
        }
        controller.close();
      },
    });
    return new Response(body, {
      status: 200,
      headers: { 'Content-Type': 'text/event-stream' },
    });
  }
});
