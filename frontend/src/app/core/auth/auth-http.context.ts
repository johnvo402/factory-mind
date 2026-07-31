import { HttpContextToken } from '@angular/common/http';

export const SKIP_AUTH_INTERCEPTOR = new HttpContextToken<boolean>(() => false);
export const AUTH_REQUEST_RETRIED = new HttpContextToken<boolean>(() => false);
