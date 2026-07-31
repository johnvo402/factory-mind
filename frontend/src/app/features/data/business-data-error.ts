import { HttpErrorResponse } from '@angular/common/http';
import { ProblemDetails } from '../../core/api/api.models';

export function businessDataErrorMessage(error: unknown): string {
  if (error instanceof HttpErrorResponse) {
    return (error.error as ProblemDetails | undefined)?.detail
      ?? `Request failed (${error.status}).`;
  }
  return 'Unable to connect to the FactoryMind API.';
}
