import { ChangeDetectionStrategy, Component, input } from '@angular/core';

export type UiIconName =
  | 'arrow-up'
  | 'arrow-up-right'
  | 'book-open'
  | 'bot'
  | 'database'
  | 'eye'
  | 'eye-off'
  | 'factory'
  | 'file-text'
  | 'log-out'
  | 'message-square'
  | 'more-horizontal'
  | 'pencil'
  | 'plus'
  | 'refresh-cw'
  | 'search'
  | 'settings'
  | 'sparkles'
  | 'trash-2'
  | 'triangle-alert'
  | 'upload'
  | 'user'
  | 'x';

@Component({
  selector: 'app-ui-icon',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '[style.--ui-icon-size.px]': 'size()' },
  template: `
    <svg
      class="ui-icon"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      stroke-width="1.8"
      stroke-linecap="round"
      stroke-linejoin="round"
      aria-hidden="true"
      focusable="false"
    >
      @switch (name()) {
        @case ('arrow-up') { <path d="m18 15-6-6-6 6"/><path d="M12 9v10"/> }
        @case ('arrow-up-right') { <path d="M7 17 17 7"/><path d="M7 7h10v10"/> }
        @case ('book-open') { <path d="M2 4h6a4 4 0 0 1 4 4v12a3 3 0 0 0-3-3H2Z"/><path d="M22 4h-6a4 4 0 0 0-4 4v12a3 3 0 0 1 3-3h7Z"/> }
        @case ('bot') { <rect width="18" height="12" x="3" y="8" rx="2"/><path d="M12 4v4"/><path d="M8 12h.01"/><path d="M16 12h.01"/><path d="M9 16h6"/> }
        @case ('database') { <ellipse cx="12" cy="5" rx="9" ry="3"/><path d="M3 5v14c0 1.7 4 3 9 3s9-1.3 9-3V5"/><path d="M3 12c0 1.7 4 3 9 3s9-1.3 9-3"/> }
        @case ('eye') { <path d="M2.1 12a10.8 10.8 0 0 1 19.8 0 10.8 10.8 0 0 1-19.8 0"/><circle cx="12" cy="12" r="3"/> }
        @case ('eye-off') { <path d="m3 3 18 18"/><path d="M10.6 10.6a2 2 0 0 0 2.8 2.8"/><path d="M9.9 4.2A10.7 10.7 0 0 1 21.9 12a11.8 11.8 0 0 1-2.4 3.5"/><path d="M6.6 6.6A11.8 11.8 0 0 0 2.1 12a10.8 10.8 0 0 0 14 6"/> }
        @case ('factory') { <path d="M2 20V9l6 3V9l6 3V4h8v16Z"/><path d="M6 20v-3"/><path d="M10 20v-3"/><path d="M14 20v-3"/><path d="M18 8h.01"/> }
        @case ('file-text') { <path d="M14.5 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V7.5Z"/><polyline points="14 2 14 8 20 8"/><path d="M8 13h8"/><path d="M8 17h5"/> }
        @case ('log-out') { <path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4"/><path d="m16 17 5-5-5-5"/><path d="M21 12H9"/> }
        @case ('message-square') { <path d="M21 15a4 4 0 0 1-4 4H8l-5 3V7a4 4 0 0 1 4-4h10a4 4 0 0 1 4 4Z"/> }
        @case ('more-horizontal') { <circle cx="12" cy="12" r="1"/><circle cx="19" cy="12" r="1"/><circle cx="5" cy="12" r="1"/> }
        @case ('pencil') { <path d="M12 20h9"/><path d="M16.5 3.5a2.1 2.1 0 0 1 3 3L8 18l-4 1 1-4Z"/> }
        @case ('plus') { <path d="M5 12h14"/><path d="M12 5v14"/> }
        @case ('refresh-cw') { <path d="M20 11a8.1 8.1 0 0 0-15.5-2M4 4v5h5"/><path d="M4 13a8.1 8.1 0 0 0 15.5 2M20 20v-5h-5"/> }
        @case ('search') { <circle cx="11" cy="11" r="8"/><path d="m21 21-4.4-4.4"/> }
        @case ('settings') { <path d="M12.2 2h-.4a2 2 0 0 0-2 2v.2a2 2 0 0 1-1 1.7l-.4.2a2 2 0 0 1-2 0l-.2-.1a2 2 0 0 0-2.7.7l-.2.4a2 2 0 0 0 .7 2.7l.2.1a2 2 0 0 1 1 1.7v.5a2 2 0 0 1-1 1.7l-.2.1a2 2 0 0 0-.7 2.7l.2.4a2 2 0 0 0 2.7.7l.2-.1a2 2 0 0 1 2 0l.4.2a2 2 0 0 1 1 1.7v.2a2 2 0 0 0 2 2h.4a2 2 0 0 0 2-2v-.2a2 2 0 0 1 1-1.7l.4-.2a2 2 0 0 1 2 0l.2.1a2 2 0 0 0 2.7-.7l.2-.4a2 2 0 0 0-.7-2.7l-.2-.1a2 2 0 0 1-1-1.7v-.5a2 2 0 0 1 1-1.7l.2-.1a2 2 0 0 0 .7-2.7l-.2-.4a2 2 0 0 0-2.7-.7l-.2.1a2 2 0 0 1-2 0l-.4-.2a2 2 0 0 1-1-1.7V4a2 2 0 0 0-2-2Z"/><circle cx="12" cy="12" r="3"/> }
        @case ('sparkles') { <path d="m12 3-1.2 3.1a2 2 0 0 1-1.1 1.1L6.5 8.5l3.2 1.2a2 2 0 0 1 1.1 1.1L12 14l1.2-3.2a2 2 0 0 1 1.1-1.1l3.2-1.2-3.2-1.3a2 2 0 0 1-1.1-1.1Z"/><path d="m5 16-.7 1.8a1 1 0 0 1-.5.5L2 19l1.8.7a1 1 0 0 1 .5.5L5 22l.7-1.8a1 1 0 0 1 .5-.5L8 19l-1.8-.7a1 1 0 0 1-.5-.5Z"/><path d="m19 15-.8 2.2a1 1 0 0 1-.6.6l-2.1.7 2.1.8a1 1 0 0 1 .6.6L19 22l.8-2.1a1 1 0 0 1 .6-.6l2.1-.8-2.1-.7a1 1 0 0 1-.6-.6Z"/> }
        @case ('trash-2') { <path d="M3 6h18"/><path d="M8 6V4h8v2"/><path d="M19 6 18 21H6L5 6"/><path d="M10 11v5"/><path d="M14 11v5"/> }
        @case ('triangle-alert') { <path d="m21.7 18-8-14a2 2 0 0 0-3.4 0l-8 14A2 2 0 0 0 4 21h16a2 2 0 0 0 1.7-3"/><path d="M12 9v4"/><path d="M12 17h.01"/> }
        @case ('upload') { <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><path d="m17 8-5-5-5 5"/><path d="M12 3v12"/> }
        @case ('user') { <path d="M19 21v-2a4 4 0 0 0-4-4H9a4 4 0 0 0-4 4v2"/><circle cx="12" cy="7" r="4"/> }
        @case ('x') { <path d="M18 6 6 18"/><path d="m6 6 12 12"/> }
      }
    </svg>
  `,
  styles: `
    :host { display: inline-grid; flex: 0 0 auto; place-items: center; }
    .ui-icon { width: var(--ui-icon-size, 20px); height: var(--ui-icon-size, 20px); display: block; }
  `,
})
export class UiIconComponent {
  readonly name = input.required<UiIconName>();
  readonly size = input(20);
}
