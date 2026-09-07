import { TestBed } from '@angular/core/testing';
import { of, Subject } from 'rxjs';
import { AiActionProposalCardComponent } from './ai-action-proposal-card.component';
import { ChatApiService } from './chat-api.service';
import { AiActionProposal, AiActionProposalStatus } from './chat.models';

describe('AiActionProposalCardComponent', () => {
  let confirmAction: jasmine.Spy;
  let cancelAction: jasmine.Spy;

  beforeEach(async () => {
    confirmAction = jasmine.createSpy('confirmAction');
    cancelAction = jasmine.createSpy('cancelAction');
    await TestBed.configureTestingModule({
      imports: [AiActionProposalCardComponent],
      providers: [{ provide: ChatApiService, useValue: { confirmAction, cancelAction } }],
    }).compileComponents();
  });

  it('renders a pending proposal without executing it', () => {
    const fixture = create('pending');
    const text = (fixture.nativeElement as HTMLElement).textContent;
    expect(text).toContain('Release PO-001');
    expect(text).toContain('Widget');
    expect(text).toContain('Revision 3');
    expect(confirmAction).not.toHaveBeenCalled();
    expect(cancelAction).not.toHaveBeenCalled();
  });

  it('confirm calls the exact persisted proposal endpoint once', async () => {
    confirmAction.and.returnValue(of({ success: true, message: 'OK', data: proposal('succeeded') }));
    const fixture = create('pending');
    (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('.confirm')!.click();
    await fixture.whenStable();
    expect(confirmAction).toHaveBeenCalledOnceWith('proposal-123');
  });

  it('cancel calls the exact persisted proposal endpoint once', async () => {
    cancelAction.and.returnValue(of({ success: true, message: 'OK', data: proposal('cancelled') }));
    const fixture = create('pending');
    (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('.cancel')!.click();
    await fixture.whenStable();
    expect(cancelAction).toHaveBeenCalledOnceWith('proposal-123');
  });

  it('disables double click while confirmation is in flight', () => {
    confirmAction.and.returnValue(new Subject());
    const fixture = create('pending');
    const button = (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('.confirm')!;
    button.click();
    fixture.detectChanges();
    button.click();
    expect(confirmAction).toHaveBeenCalledTimes(1);
    expect(button.disabled).toBeTrue();
  });

  for (const status of ['succeeded', 'expired', 'stale', 'cancelled'] as AiActionProposalStatus[]) {
    it(`disables confirmation when proposal is ${status}`, () => {
      const fixture = create(status);
      const button = (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('.confirm')!;
      expect(button.disabled).toBeTrue();
    });
  }

  function create(status: AiActionProposalStatus) {
    const fixture = TestBed.createComponent(AiActionProposalCardComponent);
    fixture.componentRef.setInput('proposal', proposal(status));
    fixture.detectChanges();
    return fixture;
  }

  function proposal(status: AiActionProposalStatus): AiActionProposal {
    return {
      proposalId: 'proposal-123',
      actionType: 'release_production_order',
      status,
      title: 'Release PO-001',
      summary: {
        productionOrderNumber: 'PO-001',
        product: 'PROD-A - Widget',
        quantity: 100,
        bomRevision: 3,
        routingRevision: 2,
      },
      expiresAt: '2099-01-01T00:00:00Z',
      failureCode: null,
    };
  }
});
