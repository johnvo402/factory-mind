import { TestBed } from '@angular/core/testing';
import { ChatMessageComponent } from './chat-message.component';
import { ChatMessage } from './chat.models';

describe('ChatMessageComponent', () => {
  it('renders sanitized Markdown and persisted citation evidence', async () => {
    await TestBed.configureTestingModule({
      imports: [ChatMessageComponent],
    }).compileComponents();
    const fixture = TestBed.createComponent(ChatMessageComponent);
    const message: ChatMessage = {
      id: 'assistant-1',
      role: 'assistant',
      content: '**Safe answer** '
        + '<img src="data:image/gif;base64,R0lGODlhAQABAAAAACw=" onerror="alert(1)"> '
        + '[S1].',
      createdAt: '2026-08-01T00:00:00Z',
      businessEvidence: [{
        referenceNumber: 1,
        entityId: 'machine-1',
        entityType: 'machine',
        title: 'MC-01 - Cutter',
        detail: 'status=available',
      }],
      citations: [{
        referenceNumber: 1,
        documentId: 'document-1',
        chunkId: 'chunk-1',
        documentTitle: 'Safety manual',
        fileName: 'safety.pdf',
        pageNumber: 8,
        excerpt: 'Wear protective equipment.',
        score: 0.94,
      }],
    };
    fixture.componentRef.setInput('message', message);
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;
    expect(element.querySelector('.markdown strong')?.textContent).toBe('Safe answer');
    expect(element.querySelector('.markdown')?.innerHTML).not.toContain('onerror');
    expect(element.querySelector('.citation summary')?.textContent).toContain('Safety manual');
    expect(element.querySelector('.citation p')?.textContent).toContain('protective equipment');
    expect(element.querySelector('.evidence-card header')?.textContent).toContain('MC-01');
    expect(element.querySelector('.evidence-card p')?.textContent).toContain('Trạng thái: Sẵn sàng');
    expect(element.querySelector('.evidence-card p')?.textContent).not.toContain('status=available');
  });
});
