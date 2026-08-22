import { HttpErrorResponse } from '@angular/common/http';

/**
 * A refused request, turned into what the message box should say.
 *
 * **The point is not to paraphrase.** Every refusal in this product carries its
 * own words — `DocumentLifecycle` writes one set for all nine document types,
 * and the services name the specifics (which items were short, which line has
 * no account). A screen that replaced those with "operation failed" would throw
 * away the only part the user can act on, so this reaches for the server's text
 * first and only invents a sentence when there is genuinely none.
 *
 * It understands the three shapes the backend actually sends:
 *
 * - `MessageResponse` — `{ message }`, from every deliberate refusal
 * - `ProblemDetails` with `errors` — ASP.NET model validation, one entry per
 *   field, each carrying the `ErrorMessage` from the Data Annotation
 * - `ProblemDetails` with `title`/`detail` — everything else
 *
 * Kept here rather than in a module's own lib because the shapes are the
 * framework's and Sales, Purchase and Accounting all receive them.
 */

/** What the server sends for a deliberate refusal. */
interface MessageResponse {
  message?: string;
}

/** ASP.NET's validation problem, and its plainer sibling. */
interface ProblemDetails {
  title?: string;
  detail?: string;
  errors?: Record<string, string[]>;
}

/** A refusal, flattened: one summary line and any lines beneath it. */
export interface ApiFailure {
  /** The HTTP status, so a caller can tell a conflict from a lost connection. */
  status: number;
  text: string;
  detail: string[];
}

const BY_STATUS: Readonly<Record<number, string>> = {
  0: 'The server could not be reached. Check your connection and try again.',
  401: 'Your session has expired. Sign in again.',
  403: 'You do not have permission to do this.',
  404: 'That record no longer exists.',
  409: 'This conflicts with something that has already happened.',
  503: 'That service is temporarily unavailable. Try again in a moment.',
};

/**
 * Reads a refusal.
 *
 * Anything that is not an `HttpErrorResponse` — a bug in the page rather than an
 * answer from the server — comes back as a generic failure at status 0, because
 * showing a stack trace in a message box helps nobody.
 */
export function readApiFailure(error: unknown): ApiFailure {
  if (!(error instanceof HttpErrorResponse)) {
    return { status: 0, text: 'Something went wrong. Try again.', detail: [] };
  }

  const body = (error.error ?? {}) as MessageResponse & ProblemDetails;

  // Model validation first: it is the only shape that has more than one thing
  // to say, and its per-field messages are the ones written on the entity.
  const fieldErrors = Object.values(body.errors ?? {}).flat().filter(Boolean);

  const text =
    body.message ??
    body.detail ??
    (fieldErrors.length > 0 ? 'Some of what was entered cannot be saved.' : undefined) ??
    body.title ??
    BY_STATUS[error.status] ??
    'Something went wrong. Try again.';

  return { status: error.status, text, detail: fieldErrors };
}
