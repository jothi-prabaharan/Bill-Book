# Reports

Every report in the product runs on one grid. Filtering, sorting, grouping, paging and exporting work the same way on all of them, so learning one report teaches you all of them.

Reports live under **Reports** in the navigation. The list shows only what your permissions allow — a report you cannot run is not listed rather than shown and refused.

## Running a report

Open a report and it runs with its default columns. Above the grid:

- **Parameters** — a date range, an as-at date. These decide *what the report is*: a movement report **for** April is a different report from one of everything filtered to April, and every opening figure depends on which you asked for.
- **Filters** — which rows survive. Add one with **+ Filter**; each applied filter shows as a chip you can clear on its own. Filters combine with *and*.
- **Grouped by** — up to three levels, each with its own subtotal row. Drag the levels to reorder them: accounts within account types reads differently from types within accounts.

## Columns

**Columns** opens the chooser. Search it — some reports offer more than thirty columns. Chosen columns sit at the top and reorder by dragging the handle. You cannot remove the last one.

## Sorting

Click a column heading to sort by it; click again to reverse; a third time clears it. **Hold Shift and click** to sort by more than one column — each shows its position, so a three-key sort is visibly a three-key sort.

Some reports set their own order and refuse to be re-sorted. A running balance is only true of one ordering, so re-sorting would leave the figures correct for an order you cannot see. The report says so when this applies.

## Totals

Subtotals and the grand total are computed over the **whole** result, not the page you are looking at. A subtotal is therefore right even when its group runs across a page boundary.

## Exporting

Two formats, and both export the whole result — every row the filters allow, not just the page on screen. Whichever you choose, the file is the report as you have it set up: the same filters, the same sorting, the same grouping or pivot, the same columns in the same order, with the paging removed.

**Export to Excel** gives you a workbook with the header frozen, amounts as numbers your spreadsheet can total, dates as real dates, and the subtotals as rows.

**Export to CSV** gives you the same thing as plain text, for loading somewhere else. Amounts come out unformatted — `1234567.89` rather than `12,34,567.89` — so whatever reads the file can parse them; dates come out as `2026-04-01`. Names in Tamil, Chinese or any other script come through intact, and open correctly in Excel.

Exports are capped at 100,000 rows. Above that the export is refused with the row count rather than quietly truncated, because a file missing its last rows looks complete.

PDF export is not offered. Reports export to Excel and CSV only.

## Sharing a report

The address bar carries your columns, filters, sorting and grouping. Copy the link and whoever opens it sees the same report — starting at the first page rather than yours. The back button and a refresh both keep your layout.

## On a phone

Below tablet width each row becomes a card, with the report's key columns as its title. Pivot is not offered at that width — a matrix has no card form.

## Balance Sheet

What the branch owns and owes at the end of a day, under **Reports › Finance** in the menu, at `/reports/balance-sheet` (TK-199). It needs **accounting.view** as well as access to reports, and has a page of its own, with no column chooser, saved views, schedule or export.

- **As at** is required and starts at today. **Compare with** adds a second column; **Same day last year** fills it in. **Show accounts with no balance** lists every active balance-sheet account, not only those with a balance.
- **Assets, Liabilities and Equity** each list their accounts by code, with a total. Every balance reads positive on its own side, so an account running against its type, such as accumulated depreciation, shows negative within its section.
- **Equity includes the profit not yet moved to an account.** **Profit of earlier years** is income less expenses before the financial year the date falls in. **Current year earnings** is income less expenses from the start of that year to the date. A year-end entry that moves profit into a retained earnings account leaves both right, because that account is then listed with its own balance.
- **An inactive account is listed while it still holds a balance.** Leaving it out would unbalance the sheet.
- **The totals** show total assets, liabilities, equity, and liabilities plus equity, and say whether assets equal liabilities plus equity. When they differ, the page says by how much: the ledger does not balance up to that date.
- Every figure is in the branch's base currency.

## Profit & Loss

What the branch earned and spent over a period, under **Reports › Finance** in the menu, at `/reports/profit-and-loss` (TK-200). It needs **accounting.view** as well as access to reports, and has a page of its own, with no column chooser, saved views, schedule or export.

- **From** and **To** are both required. They start at the beginning of the financial year and today. **Compare from** and **Compare to** add a second column, and need both dates or neither. **Same period last year** fills them in. **Show accounts with no movement** lists every active income and expense account, not only those that moved.
- **Revenue** is your sales accounts, so sales returns and discounts given are taken off it. **Cost of sales** is Cost of Goods Sold less Purchase Returns. **Gross profit** is revenue less cost of sales.
- **Other income** is every other income account, such as fee income or a foreign exchange gain. **Expenses** is every other expense account. **Net profit** is gross profit plus other income less expenses.
- Each section lists its accounts by code, with a total. Every amount reads positive on its own side, so an account running against its type, such as sales returns, shows negative and reduces its section.
- **Gross margin** and **net margin** are each profit line as a percentage of revenue, to two places. A period with no revenue shows no margin rather than a misleading number.
- The sections are split the way **Business Performance** splits them, so its margins and these agree. Net profit for a period within one financial year equals the change in **Current year earnings** on the Balance Sheet over the same days.
- Every figure is in the branch's base currency.

## Cash Flow Statement

Where the branch's cash came from and went over a period, under **Reports › Finance › Cash Flow Statement - Direct** in the menu, at `/reports/cash-flow-statement-direct` (TK-201). It needs **accounting.view** as well as access to reports, and has a page of its own, with no column chooser, saved views, schedule or export.

- **From** and **To** are both required. They start at the beginning of the financial year and today. **Compare from** and **Compare to** add a second column, and need both dates or neither. **Same period last year** fills them in.
- **Cash** is your cash, wallet, bank, overdraft and credit card accounts: the ones you set up under **Banking**. Money moved between them is not a flow, so a transfer from the bank to petty cash does not appear.
- Each movement of cash is traced to the other accounts in the same entry, and each line lists those accounts with the cash they account for. Cash in is positive, cash out negative.
- **Operating activities:**
  - **Receipts from customers** are payments against receivables, and cash sales.
  - **Payments to suppliers and employees** are payments of payables and salaries, stock, and every expense.
  - **GST and TDS paid or refunded** is tax paid to or refunded by the government. GST, TDS, rounding and an exchange gain or loss inside a receipt or payment count with it instead. So a customer who pays ₹11,800 including GST, or ₹9,800 after deducting TDS, shows that amount under receipts from customers.
  - **Other operating activities** is everything else, such as interest received or deposits.
- **Investing activities** are fixed assets bought and sold, with any gain or loss on the sale. They cover the Fixed Asset account and the accounts your fixed asset categories use. Paying a supplier's bill for an asset bought on credit counts as a payment to a supplier, because the payment itself names no asset.
- **Financing activities** are capital paid in and drawings: your equity accounts.
- **Loans cannot yet be told apart** from other liabilities, so borrowing and repaying show under other operating activities.
- **Effect of exchange rate changes** is the revaluation of foreign-currency cash and bank accounts. It changes the cash balance without any money moving, so it sits outside the three activities.
- **Cash at the beginning of the period** is the balance on the day before, plus any opening balances entered within the period, because an opening balance is not a flow. **Cash at the end of the period** is read from the ledger. The page checks that the beginning, plus the net change and the exchange effect, equals the end. When they differ, it says by how much: a journal moved cash with nothing on the other side.
- Every figure is in the branch's base currency.

## Reconciliation Report

A bank account's balance in the books against its bank statement at the end of a day, under **Reports › Finance › Reconciliation Report** in the menu, at `/reports/reconciliation-report` (TK-202). It needs **accounting.view** as well as access to reports, and has a page of its own, with no column chooser, saved views, schedule or export.

- **Bank account** and **As at** are both required. The page opens on the first active bank account and today. Every figure is in the account's own currency.
- **What counts as on the statement** is what you matched under **Banking**: a statement line matched to a spend money, receive money or transfer on that account. A line you ignored has been dealt with another way, such as a bank charge entered by journal, so it stands on neither side.
- **Balance in the books** is the account's balance in the ledger at the end of the day. For a foreign-currency account the base-currency balance is shown beneath it.
- **Payments not yet on the statement** are spend money and transfers out, dated on or before the day, that no statement line on or before the day is matched to: cheques not yet presented, for example. **Receipts not yet on the statement** are receive money and transfers in, likewise.
- **Statement lines not yet in the books** are lines on or before the day that are unmatched, or matched to a document dated after it.
- **Statement balance the books expect** is the balance in the books, plus the payments and less the receipts not yet on the statement, plus the statement lines not yet in the books.
- **Balance on the imported statement** is the latest balance the bank gave on or before the day. That is the running balance on a line, or a statement's closing balance, whichever is later, plus any lines after it. When the two balances agree the page says so. When they differ it shows the difference: match or ignore the remaining lines, or look for a payment keyed for the wrong amount or account. When no imported statement gives a balance by the day, there is nothing to compare yet.
- Each group is listed below with the date, number, reference, payee and amount of every item.
- Only money documents can be matched to a statement line. Anything else that moves the account, such as a cash sale posted to it or a journal, is in the books balance but never listed as outstanding. Ignore its statement line once it is in the books.

## Business Performance

The branch's key ratios over a period, under **Reports › Financial performance** in the menu, at `/reports/business-performance` (TK-203). It needs **accounting.view** as well as access to reports. Its page shows eight ratios, one per row:

| Ratio | How it is worked out |
|---|---|
| Gross profit margin | Revenue less cost of sales, as a percentage of revenue |
| Net profit margin | Net profit as a percentage of revenue |
| Return on investment (p.a.) | Net profit, scaled to a full year, as a percentage of net assets at the end of the period |
| Average time customers take to pay | Average receivables ÷ credit sales × the days in the period |
| Average time to pay suppliers | Average payables ÷ credit purchases × the days in the period |
| Current assets to liabilities | Current assets ÷ current liabilities, at the end of the period |
| Term assets to liabilities | Fixed assets, net of depreciation, ÷ total liabilities, at the end of the period |
| Total cash balance | Your bank and cash accounts, less overdrafts and credit cards, at the end of the period |

- **From** and **To** are both required. They start at the beginning of the financial year and today. **Compare from** and **Compare to** add a second column and a **Change** column, and need both dates or neither. **Previous period** fills them with the same number of days just before; **Same period last year** with the same days a year earlier.
- The ratios are grouped under **Profitability** (the two margins and return on investment), **Efficiency** (the payment days), **Financial position** (assets to liabilities) and **Cash**. Each shows in its own unit: a percentage, days, times or an amount.
- Under each ratio are how it is worked out and the two figures behind it, such as **₹36,000 ÷ ₹80,000 × 100**, so you can check it against the Profit & Loss and the Balance Sheet.
- **Change** is in the ratio's own unit: percentage points for a margin, days for the payment days, times for the asset ratios, and money for the cash balance.
- The ratios always appear in the order above. The page shows the number of days in each period, which the payment days and the yearly return are scaled by.

The catalog's **Business Performance** report, with its **Numerator** and **Denominator** columns, works the ratios out the same way and is still there for export and scheduled emails.

What each term means here:

- **Revenue** is your sales accounts — sales less sales returns. Foreign exchange gains and other income are not revenue, though they count towards net profit.
- **Cost of sales** is Cost of Goods Sold less Purchase Returns.
- **Credit sales** and **credit purchases** are everything invoiced to customers and billed by suppliers in the period, including GST, because the receivable and payable balances they are compared with include it too. **Average** receivables or payables is the balance at the start and the end, added and halved.
- **Fixed assets** are the Fixed Asset account and the accounts your fixed asset categories use. Every other asset is current.
- **Every liability counts as current**, because accounts cannot yet be marked as long-term. That is why *Term assets to liabilities* divides by all your liabilities.

A ratio shows no value when there is nothing to divide by — no revenue, no credit sales, no liabilities — rather than a misleading number. Return on investment also shows no value when your net assets are nil or negative.

## Fixed asset reports

Four reports read the fixed asset register, under **Fixed Assets** in the list. Each takes a **From** and **To** date; both are optional.

- **Fixed Assets Schedule** — the register as a roll-forward. For every asset: its cost at the start of the period, what was bought and disposed of during it, and its cost at the end; the same for accumulated depreciation; and the book value at both ends. Every row adds up across itself, so the column totals do too.
- **Depreciation Schedule** — each asset with its depreciation method, rate, useful life and residual value, and what was charged over the period.
- **Disposal Schedule Report** — only the assets disposed of in the period: what each cost, its book value on the day it went, what it sold for, and the result. Proceeds below book value are a **loss**; proceeds above book value are a **gain on disposal** up to the asset's cost, and a **capital gain** beyond it.
- **Fixed Asset Reconciliation** — the register read against the ledger, account by account. Every account a category uses — its asset account and its accumulated depreciation account — shows two rows: **Register**, what the register says the account should hold, and **Ledger**, what has actually been posted to it. Where the two rows differ, the books and the register disagree. This report has no totals, because adding the register to the ledger means nothing; group by **Source** to see each side on its own.

A few rules decide what appears:

- An asset still in **Draft** is not on the register and appears in none of these reports.
- An asset bought before **From** is carried in at its opening cost; one bought within the period is an addition. With no **From**, everything is an addition.
- An asset disposed of before **From** has left the register and is not shown. One disposed of after **To** was still held at the end of the period, so it is shown as held.
- Only the **book** depreciation schedule is reported, because it is the one depreciation is charged against. A tax schedule, if an asset has one, does not change these figures.

**Expect the reconciliation to show differences for now.** Registering, capitalising and disposing of an asset do not yet post to the ledger, and a purchase bill posts an asset to a single Fixed Asset account rather than to its category's. The reconciliation shows exactly those gaps, and they will close once those postings are built.

The register does not record brand, outlet, warranty expiry, cost limit or averaging method, so these reports have no columns for them.

## Budget reports

Three reports compare the branch's budget with what was posted, under **Reports › Financial performance** in the menu. They need the **Budget** add-on.

### Budget vs Actual

Budget vs Actual has its own page at `/reports/budget-vs-actual` (TK-204). Besides access to reports it needs **accounting.view**.

- **Budget**, **As at** and **Period** are all required. The page opens on the branch's own approved budget for the year holding today, today's date and **Year to date**, and runs straight away. With no approved budget for this year it takes another budget for this year, else the latest one. A draft or superseded budget says so in the list.
- **As at** must fall within the budget's year. Choosing a budget for another year moves the date to the nearer end of that year.
- **Period** is the date's month, its financial quarter, or the year to date. It always covers whole months, because a budget is monthly, and the line under the filters shows the dates it covers.
- The accounts are laid out as on the Profit & Loss: **Revenue** and **Cost of sales** to **Gross profit**, then **Other income** and **Expenses** to **Net profit**. Each account, each section's total and each profit line shows **Budget**, **Actual**, **Variance** and **Var %**.
- **Variance** is positive when it is good: income above budget, spending below it, or profit above it. A positive variance is green and a negative one red. **Var %** is the variance as a share of the budget, with a dash where nothing was budgeted.
- The actual gross and net profit are exactly what the Profit & Loss shows for the same dates. An account made inactive since still shows what was posted to it, so the two never disagree.
- Budgets are entered as positive figures, so a contra account's budget is shown with the Profit & Loss's sign. Sales returns budgeted at 1,000 read −1,000, beside actual returns that read negative too. A budget kept by project counts every project's figures under the account.
- An account with neither a budget nor anything posted in the months covered is left out.
- On a phone each row becomes a card, and each section's heading is a title above its accounts.

### Budget vs Actual by Project

Budget vs Actual by Project has its own page at `/reports/budget-vs-actual-by-project` (TK-207). It needs what Budget vs Actual needs, and a budget entered with **Budget by project** ticked.

- **Budget**, **As at** and **Period** are required and work as on Budget vs Actual. Only budgets kept by project are offered; with none, the page says how to enter one. **Project** is optional: leave it on **All projects**, or choose one.
- Each project has its own block: its accounts by section with each section's total, its **Gross profit** when it has revenue or a cost of sales, and its **Net profit**. A completed, cancelled or on-hold project says so beside its name.
- With more than one project, **All projects** at the end gives their gross and net profit added together.
- Only postings and budget lines that carry a project count. Rent with no project, for example, is on Budget vs Actual and not here. When every posting carries a project, the profit across all projects equals Budget vs Actual's.
- A project with nothing budgeted or posted to an income or expense account in the months covered is left out, such as one whose only posting is equipment bought for it.
- Variances, colours, percentages and the phone layout are as on Budget vs Actual.

### Budget Monthly Trend

Budget Monthly Trend has its own page at `/reports/budget-monthly-trend` (TK-214). It needs what Budget vs Actual needs. It shows the shape of the budget's year: the same accounts, sections and profit lines as Budget vs Actual, with a column for each month.

- **Budget** and **As at** are both required and work as on Budget vs Actual: the page opens on the branch's approved budget for this year and today's date, and runs straight away. The date must fall within the budget's year, and choosing a budget for another year moves it to the nearer end of that year.
- **Show** chooses **Budget**, **Actual** or **Variance**, or all three, which is the default. It only changes what is drawn; the figures are not asked for again.
- Each account, each section's total and the gross and net profit has a figure for each of the twelve months of the budget's year, then **Year to date** and **Full-year budget**. Every month's figures are exactly what **Budget vs Actual** shows for that month, and the year to date is its year to date, so the two reports never disagree.
- The month holding the **As at** date counts whole, as on Budget vs Actual. The months after it show their budget and no actual yet, and are greyed. **Full-year budget** is the budget for all twelve months. It is shown with **Budget** and when all three are shown.
- **Variance** is positive when it is favourable and is green, and a negative one is red, with the year to date's variance as a percentage beneath it. A contra account's budget carries the Profit & Loss's sign, as on Budget vs Actual.
- The actual net profit for the year to date is exactly what the Profit & Loss shows for the same dates. An account made inactive since it moved still shows, so the two agree.
- On a wide screen the twelve months scroll sideways inside the report's own frame, never the page. On a phone, choose a **Line**, such as Net profit or an account, and its twelve months are listed with budget, actual and variance, with the year to date at the end.

The catalog's **Budget vs Actual**, **Budget vs Actual by Project** and **Budget Monthly Trend** reports, used for export and scheduled emails, give the same figures as their pages for active accounts. Budget Monthly Trend's now needs a **Budget** and an **As at** date, and refuses a run without them, a budget that is not the branch's, or a date outside the budget's year. Until TK-207 the first two failed to run.

## Journal Report

The Journal Report has its own page at `/reports/journal-report` (TK-217), under **Reports › Accounting**. Besides access to reports it needs **accounting.view**. It is the audit view of what reached the ledger: every journal posted in a period, whether someone keyed it or an invoice, a bill, a payment or a stock adjustment made it, with its lines, who created and posted it, and its status.

- **From** and **To** are both required. The page opens on the financial year to date and runs straight away. A **To** before **From** is refused under **To**.
- **Source** narrows the report to one kind of document: manual journals, sales invoices, bills, spend money and so on. **Status** is **Posted and reversed** (the default), **Posted**, **Reversed** or **Draft**. **Account** keeps the journals that have a line on that account, each with all its lines, so it still balances.
- Above the journals, one sentence says how many there are and that debits and credits come to the same figure. When they do not, the sentence turns red and says so. The figures are of every journal the filters match, not only those on the screen.
- Each journal is a card: its number, the kind of document that posted it, its status and its date; its reference and narration; **Created by** and **Posted by** with the dates; and the journal that reverses it or that it reverses. Beneath are its lines: account, contact, description, and debit and credit in the branch's currency, with a total that balances. A line booked in another currency also shows, under its description, its amount in that currency and the rate it was booked at, such as *USD 10.00 @ 83.5*.
- A document's own posting shows the document's number. A posting written before the ledger kept numbers shows its type and id, such as `INV-42`. Its creator and poster are the user who posted it.
- A manual journal that has been reversed is **Reversed** and says *Reversed by* the journal that undid it; that journal says *Reverses* the first. Both are in the totals, which is how they cancel.
- **Draft** lists journals saved but not posted. They have not reached the ledger, so they are never in the figures of any other status, and they are numbered *Draft* with their id because they have no number yet. A draft's lines show no contact.
- With no filter, the period's debits and credits equal the **Trial balance's** movement and the **General ledger** summary's totals for the same dates.
- The page shows 25 journals at a time. **Show more journals** adds the next 25 under them, for the same filters; the totals do not change.
- On a phone each journal's lines are listed with the account on the left and its debit or credit on the right.

The catalog's **Journal Report**, used for export and scheduled emails, now needs both **From** and **To** and refuses a run without them. It lists the lines of manual journals, drafts included, with their status, and not the postings that documents make: use the page for those. Until TK-213 it failed to run on any branch with journals.

## Account Transaction

The Account Transaction report has its own page at `/reports/account-transaction` (TK-218), under **Reports › Accounting**. Besides access to reports it needs **accounting.view**. It is the detailed general ledger across many accounts at once: every posting in a period, account by account, with its contact, tax rate and a running balance.

- **From** and **To** are both required. The page opens on the financial year to date and runs straight away. A **To** before **From** is refused under **To**.
- **Source**, **Contact** and **Tax rate** narrow the postings. The tax rate shows the tax postings of that rate, whichever revision of it was in force on the day. To look at particular accounts, choose each under **Add an account**; they appear under *Only these accounts* and are taken off again with their ×, or all together with **All accounts**, up to 100. With none chosen, every account that has a posting in the period is shown.
- An account you choose is listed even when nothing was posted to it in the period, so that its balance can be read. When a contact, a tax rate or a source is chosen, only the accounts with matching postings are listed.
- Each account is a card with its code and name, a *Contra* tag when its normal balance runs opposite its type, the side its balance is on, and how many postings it has. Its grid begins with the **Opening balance**, has a row for each posting in date order, and ends with the **Closing balance** with the period's debits and credits.
- Balances are on the account's normal side, marked **Dr** or **Cr** in the heading, so a credit account no longer reads in minus. A balance-sheet account opens at everything before **From**; an income or expense account at what was posted since its financial year began, as on the **General ledger** and the **Trial balance**. With no contact, tax rate or source chosen, an account's closing balance is its closing balance there for the same dates. A narrowing filter narrows the opening balance too, so opening plus debits less credits is still the closing balance of what is shown.
- **Columns** chooses what a posting shows. The page opens with **Date**, **Document**, **Contact**, **Description**, **Debit**, **Credit** and **Balance**. Others can be added: **Source**; **Related account**, the account the other side of the document was posted to, or *Various* when it went to several; **Reference**, a manual journal's own; **Tax rate**, the rate in force on the day of a tax posting; **Permit no.**, the contact's licence that was valid on the day; and **Foreign amount**, such as *USD 10.00 @ 83.5* for a posting in another currency. Columns can be reordered by dragging, and the choice lasts while the page is open.
- Above the accounts one sentence says how many postings went to how many accounts and what the debits and credits came to, and a **Total** card at the foot repeats it. The figures are of every posting the filters match, not only those on the screen.
- The page loads 100 postings at a time. **Show more postings** adds the next 100 under them. An account that runs over a page is still one card, with its opening balance at the top and its closing balance when its last posting has loaded.
- On a phone each posting is a card, with the account's balance after it.

The catalog's **Account Transaction**, used for export and scheduled emails, now needs both **From** and **To** and refuses a run without them. Its **Tax rate** is the revision in force on the posting's day, and its **Permit no.** is a licence that was valid then. Its **Running balance** is a running total of the rows it lists, in debit terms, and does not start from the account's opening balance: read balances from the page. Until TK-213 it failed to run on any branch with postings.

## Bank Summary

The Bank Summary has its own page at `/reports/bank-summary` (TK-219), under **Reports › Accounting**. Besides access to reports it needs **accounting.view**. For each bank and cash account it shows what the account held when the period began, what came in, what went out, any revaluation of a foreign currency account, and what it holds at the end.

- **From** and **To** are both required. The page opens on the financial year to date and runs straight away. A **To** before **From** is refused under **To**. **Bank account** narrows the report to one account.
- Each account is a row with its name, its kind and number, its currency, and its **Opening**, **Received**, **Spent**, **Revaluation** and **Closing** balances in the branch's base currency, with a **Total** row when there is more than one account. Above the rows, one sentence adds the totals up: opening plus received, less spent, plus any revaluation, equals closing.
- **Opening** and **Closing** are the account's ledger balance before the period and at its end, so an account's closing balance is the balance in the books on the last day on the **Reconciliation Report** for that date.
- **Received** is the period's debits and **Spent** its credits, so a transfer between two bank accounts is spent from one and received by the other.
- **Revaluation** is the net of the period's postings on documents that also post to Unrealized FX Gain/Loss, such as a manual journal that revalues a foreign account: positive when the account is worth more in base currency. It is not counted as received or spent. Nothing posts one automatically yet.
- An account in another currency also shows its own-currency figures in smaller type under each base figure: what it held, received and spent in that currency and what it holds. A revaluation does not change them.
- A credit card or overdraft shows what is held, so what is owed reads negative.
- Every account that is switched on is listed. One switched off is listed only if it still holds a balance or had postings in the period, or if you choose it.
- On a phone each account is a card.

The catalog's **Bank Summary**, used for export and scheduled emails, now needs both **From** and **To** and refuses a run without them. It gives the page's figures, with the account's own kind (savings, current, wallet and so on) in the type column. Until TK-219 it failed to run on any branch with a bank account, and its revaluation column was always nothing.

## Project reports

Four reports read project accounting, under **Projects** in the list. Running them needs **projects.view** as well as access to reports.

- **Project Profitability**: one row per project. **Revenue** is what the project's income rows add up to, credits less debits, so a sales return or a discount given comes off it. **Cost** is its expense rows, debits less credits. **Profit** is the difference, and **Margin %** is profit as a share of revenue, left blank when there is no revenue. Rows that name no project are not counted. The report takes an optional **From** and **To** date.
- **Project Budget vs Actual**: each project's **cost budget** against the expense rows posted to it, and its tasks' **budget hours** against the hours logged, with the variance of each. Budget used % shows how much of the cost budget is spent. A budget is for the whole job, so this report has no period. A project with no cost budget shows no variance.
- **Project Unbilled Work**: what is waiting to be billed, valued the way **Add project items** on an invoice would bill it. **Time** is unbilled billable hours at the project's, the task's or the person's rate. **Expenses** are posted spend-money and bill lines marked **Charge on**, with their markup. **Milestones** are those not yet invoiced. A project with nothing waiting is left out.
- **Project Time by User**: hours logged in the period for each project task and person. The hours are split into billable, billed and non-billable.

A project's income and costs come from the ledger rows tagged with it. That covers manual journals, money documents, and sales and purchase lines. See [Projects](accounting#projects).

## Salary Increment Report

Every salary revision that takes effect in a period, under **Payroll** in the list, or straight at `/reports/salary-increment`. It needs the Payroll add-on and **payroll.view** (TK-198). Unlike the other reports it has a page of its own, so it has no column chooser, saved views, schedule or export.

- **Filters:** **From** and **To** effective dates, both required, and this financial year to begin with. **Department** and **Employee** narrow it; choosing a department leaves only its employees in the employee list. A missing date, or a to date before the from date, is shown under the field.
- **Columns:** code, employee, department and designation, effective date, **Previous CTC**, **Increment**, **Increment %**, **Revised CTC**, and who recorded the revision with its reason.
- **Salaries are annual CTC**, as Payroll stores them on each revision. There is no separate basic or gross figure to report.
- **Increment %** is the increase as a share of the previous CTC, to two places. A cut shows as negative. A revision from a CTC of zero has no percentage and shows a dash.
- **The total row** adds up previous CTC, increment and revised CTC. It shows the **average increment %**, the mean of each revision's own percentage, leaving out any from a zero CTC. On a phone each revision is a card, and the totals are the last card.
- **The department and designation are the employee's today.** A revision does not record what they were when it was made.
- **Recorded by** is the person who saved the revision. Salary revisions are not approved in the product yet, so there is no approver to show.
- Only this branch's revisions are shown.

## Restaurant reports

A Restaurant branch has eight more: day-end, item sales, category sales, waiter-wise sales, table turnover, food cost, wastage and KOT cancellations. They are listed under **Restaurant** here and on **Restaurant › Reports**, and a report whose switch is off in Restaurant settings is not listed. See [Restaurant reports](restaurant-reports).

## Custom reports

Build a report of your own from a **dataset**. The datasets are listed under **Custom** in the report list, each marked **Dataset**:

| Dataset | One row per | Needs | Custom fields |
|---|---|---|---|
| **Sales lines** | line of a posted invoice or POS sale | sales.view | the invoice's |
| **Purchase lines** | line of a posted bill | purchase.view | the bill's |
| **Ledger rows** | general-ledger line | accounting.view | none |
| **Stock movements** | stock movement in or out | inventory.view | the item's |
| **Items** | item | inventory.view | the item's |
| **Contacts** | contact | contacts.view | the contact's |

The branch's active [custom fields](masters#custom-fields) for that kind of record are extra columns at the end of the column list. They can be filtered, sorted, grouped and pivoted like any other column. Their values are shown as they are stored: a dropdown shows the option's value, and a multi-select shows its list. Amounts are in the branch's base currency, so lines in different currencies add up.

To build one, open a dataset and choose the columns, filters, grouping and sorting you want. Then choose **Save as custom report** and give it a title. For example, to see sales by salesperson and site reference, open **Sales lines**, choose the **Salesperson** and **Site reference** columns with **Total**, and group by Salesperson and then Site reference.

- A saved report appears under **Custom** in the report list and opens with its layout. **Save report** keeps changes, and **Save as a new report** keeps a copy.
- A report is yours alone unless you tick **Share with the branch**. Sharing needs **reports.edit**, and so does changing or deleting a shared report.
- A report reads its dataset, so it needs the same permission. A shared purchase-lines report is not listed for someone without **purchase.view**.
- When a custom field is deactivated, its column goes from the dataset. A saved report that used it opens without it, with a notice naming the field. Reactivating the field brings the column back.

## Scheduled reports

Any report can be emailed as an Excel workbook on a schedule. Open the report, lay it out the way you want it, and choose **Schedule**. Pick how often it goes (daily, weekly on a chosen day, or monthly on day 1 to 28), the hour, and who it goes to from the branch's users. Times are India time. Scheduling needs **reports.export**, because each run is an export.

- The workbook is the same one **Export to Excel** would give you, with the same columns, filters and grouping, and the same 100,000-row limit.
- Each run uses **your** permissions at the time it runs, not when you saved it. If you lose access to the report, or to exporting, nothing is sent. The schedule says why under **Last run** and stays in place, so the emails start again when the access comes back. A recipient who is no longer a user of the branch is left out.
- The email comes from the branch's own mailbox (Settings › Email).
- **Pause** stops a schedule without losing it. **×** deletes it. You see and change only your own schedules.
- Each schedule sends once per run, however many servers are running.

## GST returns

**GSTR-3B** for a period (`GET api/reports/gst/gstr3b?from=…&to=…`) comes from the sales register and the purchase register, in base currency, using the return's own table numbers:

| Table | What it holds |
|---|---|
| 3.1(a) | Taxed outward supplies: invoices and POS sales, less credit notes |
| 3.1(b) | Exports (place of supply outside India) |
| 3.1(c) | Nil-rated and exempt supplies |
| 3.1(d) | Purchases under reverse charge: tax you owe on them |
| 4A(3) | Credit you can claim on reverse-charge purchases |
| 4A(5) | All other credit you can claim, less debit notes |
| 4C | Net credit you can claim |
| 4D(1), 4D(2) | Credit blocked by section 17(5), and other credit you cannot claim. Shown so you can see it; never claimed |

It also shows the tax payable, which is 3.1(a), (b) and (d), and that tax less 4C, per component. The portal's order for setting credit against each tax is not applied.

**GSTR-1 export** (`GET api/reports/gst/gstr1/export?from=…&to=…`) downloads the month as JSON for the GST offline tool. It needs **reports.export**. It holds:

- **B2B**: registered buyers.
- **B2CL**: inter-state invoices to unregistered buyers over ₹1,00,000.
- **B2CS**: everything else to unregistered buyers, by place of supply and rate.
- **CDNR**: credit notes to registered buyers.
- **EXP**: exports.
- **HSN summary** and **documents issued**, with cancelled invoices counted from voided ones.

Before anything downloads, the export is checked. It must pass all of these:

- the branch has a valid GSTIN (Settings › Organization);
- every buyer GSTIN is valid;
- every line has an HSN or SAC code, and every place of supply a state code;
- the HSN summary adds up to the sections within a rupee.

If any check fails, you get the list of problems instead of a file.

Two simplifications, for now:

- A credit note to an unregistered buyer is netted into B2CS rather than reported under CDNUR.
- Filing is done by you on the portal. The export only prepares the file.

## If a branch shows no reports

The report catalog is seeded per branch. A branch created before reporting existed has an empty catalog until it is seeded, which an administrator can trigger; it only adds what is missing, so it is safe to run again.



