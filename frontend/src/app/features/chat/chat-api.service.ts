import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom, Observable } from 'rxjs';
import { ApiResponse, ProblemDetails } from '../../core/api/api.models';
import { API_ROUTES } from '../../core/api/api.routes';
import { BROWSER_FETCH } from '../../core/api/browser-fetch.token';
import { AuthService } from '../../core/auth/auth.service';
import {
  ChatCitation,
  ChatMessage,
  ChatStreamEvent,
  Conversation,
} from './chat.models';
import { parseServerSentEvents, ServerSentEvent } from './server-sent-events';

@Injectable({ providedIn: 'root' })
export class ChatApiService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly browserFetch = inject(BROWSER_FETCH);

  getConversations(): Observable<ApiResponse<Conversation[]>> {
    return this.http.get<ApiResponse<Conversation[]>>(API_ROUTES.conversations.root);
  }

  createConversation(): Observable<ApiResponse<Conversation>> {
    return this.http.post<ApiResponse<Conversation>>(API_ROUTES.conversations.root, {
      title: null,
    });
  }

  getMessages(conversationId: string): Observable<ApiResponse<ChatMessage[]>> {
    return this.http.get<ApiResponse<ChatMessage[]>>(
      API_ROUTES.conversations.messages(conversationId),
    );
  }

  streamMessage(
    conversationId: string,
    content: string,
  ): Observable<ChatStreamEvent> {
    return new Observable((subscriber) => {
      const controller = new AbortController();

      void this.consumeStream(conversationId, content, controller.signal, (event) => {
        subscriber.next(event);
      }).then(
        () => subscriber.complete(),
        (error: unknown) => {
          if (!controller.signal.aborted) {
            subscriber.error(error);
          }
        },
      );

      return () => controller.abort();
    });
  }

  private async consumeStream(
    conversationId: string,
    content: string,
    signal: AbortSignal,
    onEvent: (event: ChatStreamEvent) => void,
  ): Promise<void> {
    let response = await this.sendStreamRequest(conversationId, content, signal);

    if (response.status === 401) {
      await firstValueFrom(this.auth.refreshAccessToken());
      response = await this.sendStreamRequest(conversationId, content, signal);
    }

    if (!response.ok) {
      throw await this.createHttpError(response);
    }

    if (!response.body) {
      throw new Error('The AI response stream is unavailable.');
    }

    let completed = false;
    for await (const serverEvent of parseServerSentEvents(response.body)) {
      const event = this.toChatEvent(serverEvent);
      if (event) {
        completed ||= event.type === 'done';
        onEvent(event);
      }
    }

    if (!completed) {
      throw new Error('The AI response stream ended before completion.');
    }
  }

  private sendStreamRequest(
    conversationId: string,
    content: string,
    signal: AbortSignal,
  ): Promise<Response> {
    const accessToken = this.auth.accessToken();
    return this.browserFetch(API_ROUTES.conversations.streamMessage(conversationId), {
      method: 'POST',
      credentials: 'include',
      signal,
      headers: {
        Accept: 'text/event-stream',
        'Content-Type': 'application/json',
        ...(accessToken ? { Authorization: `Bearer ${accessToken}` } : {}),
      },
      body: JSON.stringify({ content }),
    });
  }

  private toChatEvent(serverEvent: ServerSentEvent): ChatStreamEvent | null {
    const data = JSON.parse(serverEvent.data) as Record<string, unknown>;

    switch (serverEvent.event) {
      case 'conversation':
        return typeof data['conversationId'] === 'string'
          ? { type: 'conversation', conversationId: data['conversationId'] }
          : null;
      case 'token':
        return typeof data['content'] === 'string'
          ? { type: 'token', content: data['content'] }
          : null;
      case 'citations':
        return {
          type: 'citations',
          citations: Array.isArray(data['citations'])
            ? (data['citations'] as ChatCitation[])
            : [],
        };
      case 'done':
        return { type: 'done' };
      case 'error':
        throw new Error(
          typeof data['message'] === 'string'
            ? data['message']
            : 'The AI response stream failed.',
        );
      default:
        return null;
    }
  }

  private async createHttpError(response: Response): Promise<HttpErrorResponse> {
    let error: ProblemDetails | null = null;
    try {
      error = (await response.json()) as ProblemDetails;
    } catch {
      // The status and status text still provide a useful fallback.
    }

    return new HttpErrorResponse({
      error,
      status: response.status,
      statusText: response.statusText,
      url: response.url,
    });
  }
}
