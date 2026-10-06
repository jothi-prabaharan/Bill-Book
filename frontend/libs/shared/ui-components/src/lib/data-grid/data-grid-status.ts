/**
 * What a status word means, so it is coloured the same way on every list: it
 * went well, it needs a look, it went wrong, or it is only information. A word
 * not named here reads as information rather than guessing.
 */
const STATUS_TONES: Record<string, string> = {
  paid: 'tag-success', posted: 'tag-success', approved: 'tag-success', active: 'tag-success',
  completed: 'tag-success', received: 'tag-success', delivered: 'tag-success', settled: 'tag-success',
  reconciled: 'tag-success', accepted: 'tag-success', valid: 'tag-success', open: 'tag-accent',
  sent: 'tag-accent', converted: 'tag-accent', submitted: 'tag-accent',
  pending: 'tag-warning', partial: 'tag-warning', 'partly paid': 'tag-warning', due: 'tag-warning',
  'in progress': 'tag-warning', 'ready to post': 'tag-warning', expiring: 'tag-warning',
  overdue: 'tag-danger', rejected: 'tag-danger', cancelled: 'tag-danger', failed: 'tag-danger',
  void: 'tag-danger', voided: 'tag-danger', expired: 'tag-danger', suspended: 'tag-danger',
  draft: 'tag-info', closed: 'tag-info', inactive: 'tag-info', superseded: 'tag-info',
};

/** The tag class for a status word; exported so a cell template can match the grid's own. */
export function statusTone(status: unknown): string {
  return STATUS_TONES[String(status ?? '').trim().toLowerCase()] ?? 'tag-info';
}
