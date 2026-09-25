import { ApplicationStage } from './admission.models';

/** The stage moves the server allows (S2, TK-62), for the buttons a screen offers. Admitted is reached only by Admit. */
export function nextStages(stage: ApplicationStage): ApplicationStage[] {
  const forward: ApplicationStage[] = ['Submitted', 'DocumentsVerified', 'Assessed', 'Offered'];
  if (stage === 'Admitted' || stage === 'Rejected' || stage === 'Withdrawn') {
    return [];
  }

  const later = forward.slice(forward.indexOf(stage) + 1);
  return [...later, 'Rejected', 'Withdrawn'];
}

export function canAdmit(stage: ApplicationStage): boolean {
  return stage === 'Offered';
}

export const STAGE_LABELS: Readonly<Record<ApplicationStage, string>> = {
  Submitted: 'Submitted',
  DocumentsVerified: 'Documents verified',
  Assessed: 'Assessed',
  Offered: 'Offered',
  Admitted: 'Admitted',
  Rejected: 'Rejected',
  Withdrawn: 'Withdrawn',
};
