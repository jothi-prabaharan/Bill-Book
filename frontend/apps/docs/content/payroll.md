# Payroll

Payroll core (TK-51) manages salary components, salary structures, employee CTC assignments, salary revisions, one-time adjustments, loans, and monthly payroll processing.

## Core Features

- **Salary Components**: Define earnings, deductions, and statutory heads with flat amounts, percentages, or formula expressions (`0.5 * BASIC`).
- **Salary Structures**: Group components into templates assigned to employees with annual CTC.
- **Salary Revisions & Arrears**: Back-dated salary increments automatically compute arrears differences in the subsequent pay run.
- **Pay Run Execution**: Process runs based on attendance inputs, approve, and post to General Ledger.
- **Accounting Tie**: Posting generates a balanced journal posting (`Dr Payroll Expense`, `Cr Salary Payable`, `Cr Deductions`).
- **Exports**: Generate bank disbursement CSV files and Tally XML journal vouchers.
