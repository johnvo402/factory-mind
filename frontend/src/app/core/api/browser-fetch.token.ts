import { InjectionToken } from '@angular/core';

export type BrowserFetch = typeof fetch;

export const BROWSER_FETCH = new InjectionToken<BrowserFetch>('BROWSER_FETCH', {
  providedIn: 'root',
  factory: () => globalThis.fetch.bind(globalThis),
});
