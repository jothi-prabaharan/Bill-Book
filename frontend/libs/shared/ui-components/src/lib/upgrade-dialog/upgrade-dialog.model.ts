import { PlanLimit, PlanSummary } from '@bill-book/api-client';
import { ColumnDef } from '../data-grid/data-grid.models';
import { UiMessage } from '../message-box/message-box.model';

/** What opens the upgrade prompt: a limit the server refused at, or just a button. */
export interface UpgradeDialogOptions {
  /** The refusal that sent the person here; absent when they opened it from a button. */
  limit?: PlanLimit;
  /**
   * False for someone who may not change settings. They are told to ask an
   * administrator, and the request button is not offered. Defaults to true.
   */
  canRequest?: boolean;
}

export interface UpgradeDialogState {
  options: UpgradeDialogOptions;
  /** Null when the plan could not be read; the prompt still opens without the comparison. */
  summary: PlanSummary | null;
  busy: boolean;
  /** What the request came back with, success or refusal. */
  outcome: UiMessage | null;
  /** The request went through, so the prompt has nothing more to ask. */
  requested: boolean;
  resolve: () => void;
}

export const ALREADY_ASKED = 'You have already asked to upgrade. We will contact you to complete it.';
export const ASK_AN_ADMINISTRATOR = 'Ask an administrator of your business to upgrade the plan.';

/** The comparison's columns: what is limited, what the signed-in plan allows, and what Elite does. */
export function comparisonColumns(summary: PlanSummary): ColumnDef[] {
  return [
    { field: 'label', header: 'What it allows' },
    { field: 'current', header: summary.plan, align: 'right' },
    { field: 'elite', header: 'Elite', align: 'right' },
  ];
}

export function upgradeTitle(options: UpgradeDialogOptions): string {
  return options.limit ? 'Plan limit reached' : 'Upgrade to Elite';
}

/** Whether the prompt can still send a request: allowed, one is not waiting, and there is a higher plan. */
export function canAskForUpgrade(state: UpgradeDialogState): boolean {
  return (
    state.options.canRequest !== false &&
    !state.requested &&
    state.summary?.upgradeRequested !== true &&
    state.summary?.canUpgrade !== false
  );
}

/**
 * Everything the prompt says, loudest reason first: the server's sentence about
 * the limit, then why there is nothing to press, then what the request came
 * back with.
 */
export function upgradeMessages(state: UpgradeDialogState): UiMessage[] {
  const messages: UiMessage[] = [];

  if (state.options.limit) {
    messages.push({ tone: 'warning', text: state.options.limit.message });
  }

  if (state.options.canRequest === false) {
    messages.push({ tone: 'info', text: ASK_AN_ADMINISTRATOR });
  } else if (state.summary?.upgradeRequested === true && !state.requested) {
    messages.push({ tone: 'info', text: ALREADY_ASKED });
  }

  if (state.outcome) {
    messages.push(state.outcome);
  }

  return messages;
}
