import { HttpErrorResponse } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { firstValueFrom, Subscription } from 'rxjs';
import { ApiResponse, ProblemDetails } from '../../core/api/api.models';
import { ChatApiService } from './chat-api.service';
import { ChatMessage, ChatStreamEvent, Conversation } from './chat.models';

@Injectable({ providedIn: 'root' })
export class ChatStore {
  private readonly api = inject(ChatApiService);
  private readonly conversationsState = signal<Conversation[]>([]);
  private readonly messagesState = signal<ChatMessage[]>([]);
  private readonly selectedConversationIdState = signal<string | null>(null);
  private readonly loadingState = signal(false);
  private readonly streamingState = signal(false);
  private readonly errorState = signal('');
  private activeStream: Subscription | null = null;
  private resolveActiveStream: (() => void) | null = null;
  private initialized = false;
  private localMessageSequence = 0;
  private sessionVersion = 0;

  readonly conversations = this.conversationsState.asReadonly();
  readonly messages = this.messagesState.asReadonly();
  readonly selectedConversationId = this.selectedConversationIdState.asReadonly();
  readonly isLoading = this.loadingState.asReadonly();
  readonly isStreaming = this.streamingState.asReadonly();
  readonly error = this.errorState.asReadonly();
  readonly selectedConversation = computed(() =>
    this.conversationsState().find((conversation) =>
      conversation.id === this.selectedConversationIdState()) ?? null,
  );

  async initialize(): Promise<void> {
    if (this.initialized || this.loadingState()) {
      return;
    }

    const version = this.sessionVersion;
    this.loadingState.set(true);
    this.errorState.set('');
    try {
      const conversations = this.requireData(
        await firstValueFrom(this.api.getConversations()),
      );
      if (!this.isCurrentSession(version)) {
        return;
      }

      this.conversationsState.set(conversations);
      this.initialized = true;

      if (conversations.length > 0) {
        await this.selectConversation(conversations[0].id, version);
      }
    } catch (error) {
      if (this.isCurrentSession(version)) {
        this.errorState.set(this.errorMessage(error));
      }
    } finally {
      if (this.isCurrentSession(version)) {
        this.loadingState.set(false);
      }
    }
  }

  async selectConversation(
    conversationId: string,
    version = this.sessionVersion,
  ): Promise<void> {
    if (this.streamingState()) {
      return;
    }

    this.selectedConversationIdState.set(conversationId);
    this.messagesState.set([]);
    this.loadingState.set(true);
    this.errorState.set('');
    try {
      await this.reloadMessages(conversationId, version);
    } catch (error) {
      if (this.isCurrentSession(version)) {
        this.errorState.set(this.errorMessage(error));
      }
    } finally {
      if (this.isCurrentSession(version)) {
        this.loadingState.set(false);
      }
    }
  }

  startNewConversation(): void {
    if (this.streamingState()) {
      return;
    }

    this.selectedConversationIdState.set(null);
    this.messagesState.set([]);
    this.errorState.set('');
  }

  async sendMessage(rawContent: string): Promise<void> {
    const content = rawContent.trim();
    if (!content || this.streamingState()) {
      return;
    }

    const version = this.sessionVersion;
    let conversationId: string | null = null;
    this.streamingState.set(true);
    this.errorState.set('');

    try {
      conversationId = await this.ensureConversation(version);
      if (!this.isCurrentSession(version)) {
        return;
      }

      const userMessage = this.createLocalMessage('user', content);
      const assistantMessage = this.createLocalMessage('assistant', '');
      this.messagesState.update((messages) => [
        ...messages,
        userMessage,
        assistantMessage,
      ]);

      await this.consumeMessageStream(conversationId, assistantMessage.id);
      if (!this.isCurrentSession(version)) {
        return;
      }

      await Promise.all([
        this.reloadMessages(conversationId, version),
        this.reloadConversations(version),
      ]);
    } catch (error) {
      if (this.isCurrentSession(version)) {
        this.removeLocalAssistantMessage();
        if (conversationId) {
          try {
            await this.reloadMessages(conversationId, version);
          } catch {
            // Preserve the original stream error when reconciliation also fails.
          }
        }
        this.errorState.set(this.errorMessage(error));
      }
    } finally {
      if (this.isCurrentSession(version)) {
        this.activeStream = null;
        this.resolveActiveStream = null;
        this.streamingState.set(false);
      }
    }
  }

  reset(): void {
    this.sessionVersion += 1;
    this.activeStream?.unsubscribe();
    this.resolveActiveStream?.();
    this.activeStream = null;
    this.resolveActiveStream = null;
    this.initialized = false;
    this.conversationsState.set([]);
    this.messagesState.set([]);
    this.selectedConversationIdState.set(null);
    this.loadingState.set(false);
    this.streamingState.set(false);
    this.errorState.set('');
  }

  private async ensureConversation(version: number): Promise<string> {
    const selectedConversationId = this.selectedConversationIdState();
    if (selectedConversationId) {
      return selectedConversationId;
    }

    const conversation = this.requireData(
      await firstValueFrom(this.api.createConversation()),
    );
    if (!this.isCurrentSession(version)) {
      return conversation.id;
    }

    this.conversationsState.update((conversations) => [
      conversation,
      ...conversations,
    ]);
    this.selectedConversationIdState.set(conversation.id);
    return conversation.id;
  }

  private consumeMessageStream(
    conversationId: string,
    assistantMessageId: string,
  ): Promise<void> {
    return new Promise((resolve, reject) => {
      const resolveStream = () => {
        this.resolveActiveStream = null;
        resolve();
      };
      this.resolveActiveStream = resolveStream;
      this.activeStream = this.api
        .streamMessage(conversationId, this.latestUserContent())
        .subscribe({
          next: (event) => this.applyStreamEvent(assistantMessageId, event),
          error: (error: unknown) => {
            this.resolveActiveStream = null;
            reject(error);
          },
          complete: resolveStream,
        });
    });
  }

  private applyStreamEvent(
    assistantMessageId: string,
    event: ChatStreamEvent,
  ): void {
    if (event.type === 'token') {
      this.updateMessage(assistantMessageId, (message) => ({
        ...message,
        content: message.content + event.content,
      }));
    } else if (event.type === 'citations') {
      this.updateMessage(assistantMessageId, (message) => ({
        ...message,
        citations: event.citations,
      }));
    }
  }

  private updateMessage(
    messageId: string,
    update: (message: ChatMessage) => ChatMessage,
  ): void {
    this.messagesState.update((messages) =>
      messages.map((message) =>
        message.id === messageId ? update(message) : message),
    );
  }

  private latestUserContent(): string {
    const messages = this.messagesState();
    for (let index = messages.length - 1; index >= 0; index -= 1) {
      if (messages[index].role === 'user') {
        return messages[index].content;
      }
    }

    return '';
  }

  private async reloadMessages(
    conversationId: string,
    version: number,
  ): Promise<void> {
    const messages = this.requireData(
      await firstValueFrom(this.api.getMessages(conversationId)),
    );
    if (
      this.isCurrentSession(version)
      && this.selectedConversationIdState() === conversationId
    ) {
      this.messagesState.set(messages);
    }
  }

  private async reloadConversations(version: number): Promise<void> {
    const conversations = this.requireData(
      await firstValueFrom(this.api.getConversations()),
    );
    if (this.isCurrentSession(version)) {
      this.conversationsState.set(conversations);
    }
  }

  private createLocalMessage(
    role: 'user' | 'assistant',
    content: string,
  ): ChatMessage {
    this.localMessageSequence += 1;
    return {
      id: `local-${role}-${this.localMessageSequence}`,
      role,
      content,
      createdAt: new Date().toISOString(),
      citations: [],
    };
  }

  private removeLocalAssistantMessage(): void {
    this.messagesState.update((messages) =>
      messages.filter((message) => !message.id.startsWith('local-assistant-')),
    );
  }

  private requireData<T>(response: ApiResponse<T>): T {
    if (!response.success || response.data == null) {
      throw new Error(response.message || 'The FactoryMind API returned an invalid response.');
    }

    return response.data;
  }

  private errorMessage(error: unknown): string {
    if (error instanceof HttpErrorResponse) {
      const problem = error.error as ProblemDetails | null;
      return problem?.detail ?? `Request failed (${error.status}).`;
    }

    return error instanceof Error
      ? error.message
      : 'Unable to complete the chat request.';
  }

  private isCurrentSession(version: number): boolean {
    return version === this.sessionVersion;
  }
}
