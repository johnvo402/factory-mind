import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { DialogFocusDirective } from './dialog-focus.directive';

@Component({
  imports: [DialogFocusDirective],
  template: `
    <button id="trigger" type="button" (click)="open.set(true)">Mở</button>
    @if (open()) {
      <section appDialogFocus (dialogDismissed)="open.set(false)" role="dialog">
        <button id="first" type="button">Đầu</button>
        <button id="last" type="button">Cuối</button>
      </section>
    }
  `,
})
class DialogHostComponent {
  readonly open = signal(false);
}

describe('DialogFocusDirective', () => {
  it('dismisses with Escape and returns focus to the trigger', async () => {
    await TestBed.configureTestingModule({ imports: [DialogHostComponent] }).compileComponents();
    const fixture = TestBed.createComponent(DialogHostComponent);
    fixture.detectChanges();

    const trigger = fixture.nativeElement.querySelector('#trigger') as HTMLButtonElement;
    trigger.focus();
    trigger.click();
    fixture.detectChanges();

    const dialog = fixture.nativeElement.querySelector('[role="dialog"]') as HTMLElement;
    dialog.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    fixture.detectChanges();

    expect(fixture.componentInstance.open()).toBeFalse();
    expect(document.activeElement).toBe(trigger);
  });
});
