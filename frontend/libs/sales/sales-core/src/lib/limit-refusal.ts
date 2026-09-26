import { HttpErrorResponse } from '@angular/common/http';

/**
 * Whether a refused save may be sent to an approver instead (TK-102): the
 * server says so on a save refused at the credit or discount limit, and the
 * form then offers "Request approval", which saves again with
 * `requestApproval: true`.
 */
export function canRequestApproval(error: unknown): boolean {
  return (
    error instanceof HttpErrorResponse &&
    typeof error.error === 'object' &&
    error.error !== null &&
    (error.error as { canRequestApproval?: unknown }).canRequestApproval === true
  );
}
