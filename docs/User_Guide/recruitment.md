# Recruitment and Onboarding

Recruitment (TK-57, H10) covers the full talent acquisition lifecycle from requisition approvals, job openings, applicant pipeline tracking, interview scheduling and evaluations, through to candidate offers and idempotent employee onboarding.

## Core Features

- **Job Requisitions**: Departmental headcount requests with budgetary min/max CTC, justification, replacement flags, and approval workflow routing.
- **Job Openings**: Published job descriptions linked to approved requisitions with lifecycle status controls (Draft, Open, OnHold, Closed, Filled).
- **Candidate Pool**: Centralized repository of candidate resumes, expected CTC, notice period, and referral tracking with unique email deduplication per branch.
- **Application Pipeline Board**: Reactive stages (Applied, Screening, Interview, Offer, Hired, Rejected) for candidate progression with reason tracking.
- **Interview Rounds**: Schedule multi-round interviews (Telephonic, Technical, HR, Managerial) with rating scores and structured interviewer feedback.
- **Offers & Idempotent Onboarding**: Generate compensation offers linked to salary structures. When an offer is accepted, the system idempotently creates the employee in HRM with onboarding checklists and configures their salary structure in Payroll without duplicating on retry.
