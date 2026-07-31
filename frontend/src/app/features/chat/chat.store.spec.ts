import { TestBed } from '@angular/core/testing';
import { Observable, of, Subject, throwError } from 'rxjs';
import { ApiResponse } from '../../core/api/api.models';
import { ChatApiService } from './chat-api.service';
import { ChatMessage, ChatStreamEvent, Conversation } from './chat.models';
import { ChatStore } from './chat.store';

describe('ChatStore', () => {
  let store: ChatStore;
  let api: jasmine.SpyObj<ChatApiService>;

  const conversation = (title = 'New conversation'): Conversation => ({
    id: 'conversation-1',
    title,
    createdAt: '2026-08-01T00:00:00Z',
    updatedAt: '2026-08-01T00:00:00Z',
  });

  beforeEach(() => {
    api = jasmine.createSpyObj<ChatApiService>('ChatApiService', [
      'getConversations',
      'createConversation',
      'getMessages',
      'streamMessage',
    ]);
    TestBed.configureTestingModule({
      providers: [ChatStore, { provide: ChatApiService, useValue: api }],
    });
    store = TestBed.inject(ChatStore);
  });

  it('loads the most recent conversation and its persisted messages', async () => {
    const message = persistedAssistantMessage();
    api.getConversations.and.returnValue(success([conversation('Safety notes')]));
    api.getMessages.and.returnValue(success([message]));

    await store.initialize();

    expect(store.selectedConversationId()).toBe('conversation-1');
    expect(store.selectedConversation()?.title).toBe('Safety notes');
    expect(store.messages()).toEqual([message]);
  });

  it('creates a conversation, applies stream updates and reloads canonical history', async () => {
    const canonicalMessages: ChatMessage[] = [
      {
        id: 'user-1',
        role: 'user',
        content: 'Which machine is ready?',
        createdAt: '2026-08-01T00:00:01Z',
        citations: [],
      },
      persistedAssistantMessage(),
    ];
    api.getConversations.and.returnValues(
      success([]),
      success([conversation('Which machine is ready?')]),
    );
    api.createConversation.and.returnValue(success(conversation()));
    api.getMessages.and.returnValue(success(canonicalMessages));
    const streamEvents: ChatStreamEvent[] = [
      { type: 'conversation', conversationId: 'conversation-1' },
      { type: 'token', content: 'Machine A is ready ' },
      { type: 'token', content: '[S1].' },
      { type: 'citations', citations: persistedAssistantMessage().citations },
      { type: 'done' },
    ];
    api.streamMessage.and.returnValue(of(...streamEvents));

    await store.initialize();
    await store.sendMessage('Which machine is ready?');

    expect(api.createConversation).toHaveBeenCalledTimes(1);
    expect(api.streamMessage).toHaveBeenCalledWith(
      'conversation-1',
      'Which machine is ready?',
    );
    expect(store.messages()).toEqual(canonicalMessages);
    expect(store.selectedConversation()?.title).toBe('Which machine is ready?');
    expect(store.isStreaming()).toBeFalse();
  });

  it('ignores a history response from a workspace session that was reset', async () => {
    const pending = new Subject<ApiResponse<Conversation[]>>();
    api.getConversations.and.returnValues(pending, success([]));

    const initialization = store.initialize();
    store.reset();
    pending.next({ success: true, message: 'OK', data: [conversation('Old session')] });
    pending.complete();
    await initialization;

    expect(store.conversations()).toEqual([]);
    expect(store.selectedConversationId()).toBeNull();

    await store.initialize();
    expect(api.getConversations).toHaveBeenCalledTimes(2);
  });

  it('reconciles optimistic messages with persisted history after a stream failure', async () => {
    api.getConversations.and.returnValue(success([]));
    api.createConversation.and.returnValue(success(conversation()));
    api.streamMessage.and.returnValue(throwError(() => new Error('AI unavailable')));
    api.getMessages.and.returnValue(success([]));

    await store.initialize();
    await store.sendMessage('Question that failed');

    expect(store.messages()).toEqual([]);
    expect(store.error()).toBe('AI unavailable');
  });

  function persistedAssistantMessage(): ChatMessage {
    return {
      id: 'assistant-1',
      role: 'assistant',
      content: 'Machine A is ready [S1].',
      createdAt: '2026-08-01T00:00:02Z',
      citations: [{
        referenceNumber: 1,
        documentId: 'document-1',
        chunkId: 'chunk-1',
        documentTitle: 'Machine handbook',
        fileName: 'machine-handbook.pdf',
        pageNumber: 4,
        excerpt: 'Machine A is available for operation.',
        score: 0.91,
      }],
    };
  }

  function success<T>(data: T): Observable<ApiResponse<T>> {
    return of({ success: true, message: 'OK', data });
  }
});
