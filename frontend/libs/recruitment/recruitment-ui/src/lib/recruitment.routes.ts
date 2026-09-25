import { Routes } from '@angular/router';
import { CandidatesPage } from './candidates/candidates.page';
import { InterviewsPage } from './interviews/interviews.page';
import { OffersPage } from './offers/offers.page';
import { OpeningsPage } from './openings/openings.page';
import { PipelinePage } from './pipeline/pipeline.page';
import { RequisitionsPage } from './requisitions/requisitions.page';

export const recruitmentRoutes: Routes = [
  {
    path: 'recruitment',
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'pipeline' },
      { path: 'pipeline', component: PipelinePage, data: { access: { permission: 'recruitment.view' } } },
      { path: 'requisitions', component: RequisitionsPage, data: { access: { permission: 'recruitment.view' } } },
      { path: 'openings', component: OpeningsPage, data: { access: { permission: 'recruitment.view' } } },
      { path: 'candidates', component: CandidatesPage, data: { access: { permission: 'recruitment.view' } } },
      { path: 'interviews', component: InterviewsPage, data: { access: { permission: 'recruitment.view' } } },
      { path: 'offers', component: OffersPage, data: { access: { permission: 'recruitment.view' } } },
    ],
  },
];
