import { DOCUMENT } from '@angular/common';
import {
  AfterViewInit,
  Directive,
  ElementRef,
  HostListener,
  inject,
  OnDestroy,
  output,
} from '@angular/core';

const FOCUSABLE_SELECTOR = [
  'a[href]',
  'button:not([disabled])',
  'input:not([disabled])',
  'select:not([disabled])',
  'textarea:not([disabled])',
  '[tabindex]:not([tabindex="-1"])',
].join(',');

@Directive({
  selector: '[appDialogFocus]',
})
export class DialogFocusDirective implements AfterViewInit, OnDestroy {
  readonly dialogDismissed = output<void>();

  private readonly element = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly document = inject(DOCUMENT);
  private readonly returnFocusTo = this.document.activeElement as HTMLElement | null;
  private readonly previousBodyOverflow = this.document.body.style.overflow;

  ngAfterViewInit(): void {
    const dialog = this.element.nativeElement;
    if (!dialog.hasAttribute('tabindex')) {
      dialog.tabIndex = -1;
    }

    this.document.body.style.overflow = 'hidden';
    const initialFocus = dialog.querySelector<HTMLElement>('[autofocus]') ?? this.focusableItems()[0];
    (initialFocus ?? dialog).focus();
  }

  ngOnDestroy(): void {
    this.document.body.style.overflow = this.previousBodyOverflow;
    if (this.returnFocusTo?.isConnected) {
      this.returnFocusTo.focus();
    }
  }

  @HostListener('keydown', ['$event'])
  protected onKeydown(event: KeyboardEvent): void {
    if (event.key === 'Escape') {
      event.preventDefault();
      this.dialogDismissed.emit();
      return;
    }

    if (event.key !== 'Tab') {
      return;
    }

    const focusableItems = this.focusableItems();
    if (focusableItems.length === 0) {
      event.preventDefault();
      this.element.nativeElement.focus();
      return;
    }

    const first = focusableItems[0];
    const last = focusableItems.at(-1)!;
    if (event.shiftKey && this.document.activeElement === first) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && this.document.activeElement === last) {
      event.preventDefault();
      first.focus();
    }
  }

  private focusableItems(): HTMLElement[] {
    return Array.from(this.element.nativeElement.querySelectorAll<HTMLElement>(FOCUSABLE_SELECTOR)).filter(
      element => element.offsetParent !== null,
    );
  }
}
