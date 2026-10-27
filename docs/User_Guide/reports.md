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

## Dates and amounts

A report shows dates and amounts the way your branch is set up, not in a format of its own. The date pattern is the **Date Format** in **Settings › Configuration** (`dd/MM/yyyy`, `yyyy-MM-dd` and so on), and the amounts follow your base currency in **Settings › Currencies**: its symbol, on the side it is written, the grouping (`12,34,567.89` or `1,234,567.89`) and its decimal places. Every amount keeps its decimals, so a whole figure reads `80,000.00` and never `80,000`.

- **The date fields** above a report, the dates in its rows and the period it names in a sentence all use the same pattern.
- **An amount in another currency**, such as `USD 1,000.00 @ 83.5` under a posting or `150.00 EUR` beside a document, is written with your grouping and decimal places and its own currency code, not your branch's symbol.
- **On the reports that have no page of their own**, the amount columns show the number without a symbol: the report's heading names the currency, and a few columns are in the currency of each document.

What is not changed: the **document registers** (invoices, bills and the other lists) keep their own compact look, quantity, rate and percentage columns keep their own decimals, and the **Excel** and **CSV** files keep the plain form described below, so a spreadsheet can read them.

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

### Fixed Assets Schedule

The Fixed Assets Schedule has its own page at `/reports/fixed-assets-schedule` (TK-252), under **Reports > Fixed Asset**. Besides access to reports and **accounting.view**, it requires the **FixedAssets** add-on alone. It displays the fixed asset register roll-forward over a required period.

- **From** and **To** dates are both required (D-67). The page opens on the current financial year to date (`rangeForPreset('this-financial-year')` to today). A To date earlier than From date is refused with an inline error under To.
- **Filters:** Optional **Category**, **Status** (All, Active, Disposed), **Depreciation Book** (Books / Tax), **Search** (matching code, name, or serial number), and a **Show Disposal Details** column chooser toggle.
- **Summary Metrics:** At the top of the report, metric cards display **Total Assets**, **Opening Cost**, **Additions**, **Disposals**, **Closing Cost**, **Depreciation Charged**, **Closing Accum Dep**, and **Closing Net Book Value**.
- **Grouped Layout:** Assets are grouped by category with category name, Asset Account, and Accumulated Depreciation Account metadata. Each category displays asset-by-asset cost roll-forward, depreciation roll-forward, and closing book values, followed by category subtotals.
- **Grand Total:** A prominent summary card aggregates roll-forwards across all categories.
- **Disposal Details:** Toggling disposal details surfaces disposal date, sale proceeds, and gain/loss on disposal.
- **Catalog Source:** The catalog's **Fixed Assets Schedule** report (`fixed-assets-schedule`) marks both From and To parameters as required (`IsRequired = true`) and requires `AddOn.FixedAssets`.

### Depreciation Schedule

The Depreciation Schedule has its own page at `/reports/depreciation-schedule` (TK-253), under **Reports > Fixed Asset**. Besides access to reports and **accounting.view**, it requires the **FixedAssets** add-on alone. It displays each asset's depreciation method, rate, useful life, residual value, and the calculated depreciation charge over the selected period.

- **From** and **To** dates are both required (D-67). The page opens on the current financial year to date (`rangeForPreset('this-financial-year')` to today). A To date earlier than From date is refused with an inline error under To.
- **Filters:** Optional **Category**, **Depreciation Method** (Straight Line, Written Down Value), **Depreciation Book** (Books / Tax), and **Search Asset** (matching asset code, name, or serial number).
- **Summary Cards:** Top metric cards display **Total Assets**, **Total Asset Cost**, **Opening Accum Dep** (before From date), **Period Depreciation** (matches P&L Expense for the period), **Written Back (Disposals)** (accumulated depreciation removed on disposed assets), **Closing Accum Dep** (as at To date), and **Closing Net Book Value** (carrying value on balance sheet).
- **Category Grouping & Metadata:** Assets are grouped by category with header badges showing the category's Asset Account, Accumulated Depreciation Account, Depreciation Expense Account, and the category's total period depreciation charge.
- **Asset Roll-Forward Grid:** Each category contains a structured table showing Asset Code, Asset Name with serial number, Depreciation Method, Depreciation Start Date, Rate / Effective Life, Residual Value, Asset Cost, Opening Accum Dep, Period Charge, Disposals Accum Dep, Closing Accum Dep, and Closing NBV.
- **Responsive Layout:** On mobile displays (< 800px), grids transform into responsive asset card lists with full roll-forward breakdowns and quick-action summaries.
- **Catalog Source:** The catalog's **Depreciation Schedule** report (`depreciation-schedule`) marks both From and To parameters as required (`IsRequired = true`) and requires `AddOn.FixedAssets`.

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

![Account Transaction](assets/screenshots/report-account-transaction.png)

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

## Foreign Currency Gain or Loss

![Foreign Currency Gain or Loss](assets/screenshots/report-foreign-currency-gain-or-loss.png)

The Foreign Currency Gain or Loss report has its own page at `/reports/foreign-currency-gain-or-loss` (TK-220), under **Reports › Accounting**. Besides access to reports it needs **accounting.view**. It shows what exchange rates have gained or lost on the branch's foreign-currency balances: **realized** when a settlement posted a difference, and **unrealized** on what is still open, at a rate you give for each currency. It posts nothing.

- **From** and **As at** are both required. The page opens on the financial year to date, as at today, and runs straight away. An **As at** before **From** is refused under **As at**. **Currency** narrows the report to one currency; it lists the currencies the branch holds a balance in at the as-at date.
- **Revaluation rates.** Under the filters there is one rate for each currency with a balance, in base currency for one unit of it (for example 84.50 INR for 1 USD). Each starts from the **rate master's** rate for the as-at date and can be changed there. A currency with no rate on file starts empty and says so. Change a rate and choose **Run report** to see the effect. A rate must be above nothing; a refused one is shown under its own field.
- **Balances.** A balance is what the books hold in the currency, on the **receivable** and **payable** accounts and on a **bank or cash account kept in a foreign currency**: money owed to the branch is positive and money it owes is negative. The report shows it as **Due in currency**, then **At booked rates** (the same balance in base currency at the rates the documents were booked at, never repriced from a rate looked up now) and **At revaluation rate** (the balance times the rate you gave).
- **Unrealized** is the second less the first, whichever side of the books the balance is on: a rising rate is a gain on a receivable and a loss on a payable. It is the difference from the booked rates, whether or not a revaluation journal has been posted.
- **Realized** is what settlements between the **From** and **As at** dates posted to **Realized FX Gain/Loss**, a gain positive and a loss negative. Each posting is tied to the currency and account of the document that settled the balance: the receivable or payable first, else the bank account. **Its total is that account's movement on the Profit & Loss for the same dates.** A posting made with no foreign balance, such as a manual journal, belongs to no currency and is shown on its own line, **Not tied to a currency**, so the total still agrees.
- **Net** is realized plus unrealized. A currency with no rate has no revalued or unrealized figure, shows **no rate** in its place, is named in a warning above the grid, and its net is its realized part only.
- Each currency is a block: a heading with its rate, its accounts, and **Total** for the currency when it has more than one account, then the totals of all currencies. Above the grid one sentence adds the totals up: realized plus unrealized equals net. Losses are shown in red.
- On a phone each currency is a card.

The catalog's **Foreign Currency Gain or Loss**, used for export, saved views and scheduled emails, now needs both **From** and **As at** and refuses a run without them. It gives one row for each account and currency with the due amount in the currency, the amount at booked rates and the realized figure for the period, plus a row for postings no currency can claim, so its rows add up to the same realized total. The revaluation rate, the revalued amount, the unrealized and net figures and the exposure and year-to-date columns are left empty there, because a rate is something you give for each run on the page. Until TK-220 it failed to run on any branch with a foreign-currency balance.

## Foreign Currency Gain or Loss Details

![Foreign Currency Gain or Loss Details](assets/screenshots/report-foreign-currency-gain-or-loss-details.png)

The Foreign Currency Gain or Loss Details report has its own page at `/reports/foreign-currency-gain-or-loss-details` (TK-221), under **Reports › Accounting**. Besides access to reports it needs **accounting.view**. It shows the gains and losses of the **Foreign Currency Gain or Loss** report document by document: which settlement made each realized amount, and which open document carries each unrealized one. Its totals for each currency and account are that report's for the same dates and rates, worked out by the same code. It posts nothing.

- **From** and **To** are both required. The page opens on the financial year to date and runs straight away. A **To** before **From** is refused under **To**. Realized covers **From** to **To**; open documents and unrealized are as at **To**. **Currency**, **Contact** and **Account** narrow the report; each lists only what has a foreign-currency posting up to **To**, and a contact or account that is not the branch's is refused at its own field.
- **Revaluation rates** are as on the summary report: one for each currency that still has a balance, in base currency for one unit of it, starting from the **rate master's** rate for the **To** date and changeable on the page. A currency with no rate says so, its documents show no unrealized figure, and a warning names it.
- **Open documents.** A document is listed with what is still owed on it at **To**: an invoice of USD 1,000 settled in part by receipts shows what is left, because each receipt's line says which document it settles and comes off it at the rate that document was booked at. A receipt that settles nothing in particular, an **advance**, is a document of its own with a negative balance. A document that nets to nothing is not listed. A foreign bank account's postings are each their own document. A document with no number of its own is shown by its kind and its ledger id.
- On an open document the **Rate** column reads the rate it was booked at, then the revaluation rate. **Amount** is what is owed in the currency, negative for a payable; **In base** is that amount at the booked rate, with the amount at the revaluation rate in smaller type under it. **Unrealized** is the difference, a gain on a receivable when the rate rises and a loss on a payable.
- **Settlements.** Each exchange difference posted in the period is a row on the date it was posted, tagged **Settlement**, with the document it settled (for example *Settles INV-0001*), the amount settled, the rate that document was booked at and the rate the money moved at, and the **Realized** gain or loss, positive a gain. A settlement in the period of a document that is now settled in full is still listed, so every realized amount can be traced.
- Documents are listed under their currency and account (receivable, payable or bank), by date, with **Total** for the account when it has more than one document and for the currency when it has more than one account, then the totals of all currencies. A posting to Realized FX Gain/Loss that has no foreign balance behind it, such as a manual journal, belongs to no currency and is listed under **Not tied to a currency**; it is in the grand total, so the realized total is still the account's movement on the Profit & Loss. Choosing a currency, contact or account leaves it out.
- A document's amounts are rounded on their own, so an account's rows can differ from the account's total, which is rounded once, by a paisa or two. The account and currency totals are always those of the summary report.
- Above the grid one sentence adds the totals up: realized plus unrealized equals net. On a phone each currency is a card.

The catalog's **Foreign Currency Gain or Loss Details**, used for export, saved views and scheduled emails, now needs both **From** and **To** and refuses a run without them. It lists the ledger's postings rather than netting a settlement against the document it settles: one row for each foreign-currency balance posting up to **To**, with the amount in the currency and in base at the booked rate, and one for each realized difference in the period, so its rows add up to the same figures for each currency. The revaluation, unrealized, net, exposure and year-to-date columns are left empty there, because a rate is something you give for each run on the page. Until TK-221 it failed to run on any branch with a foreign-currency balance.

## Aged Receivables Summary

![Aged Receivables Summary](assets/screenshots/report-aged-receivables-summary.png)

The Aged Receivables Summary has its own page at `/reports/aged-receivables-summary` (TK-222), under **Reports › Sales Reports**. Besides access to reports it needs **sales.view**. It shows what each customer owed on a date and how overdue it was, in the branch's base currency.

- **As at** is required. The page opens as at today and runs straight away; a missing date is refused under the field.
- **Age by** counts an invoice's age from its **due date** (the default) or its **invoice date**. An invoice with no due date counts from its invoice date, and a document that is not an invoice, such as an opening balance or a manual journal, from the day it was posted.
- **Column width** is 30 days (the default), 15 or 7. There are always five columns: **Current** (not yet due, or due on the date), then four overdue columns, the last being everything beyond three widths: with 30 days, 1-30, 31-60, 61-90 and over 90 days.
- **Customer** narrows the report to one customer. **Show customers with nothing owed** also lists the customers with nothing open.
- Each customer is a row with its code, the five columns and a **Total**, and a **Total** row closes the table. A sentence above says how much is owed and how much of it is overdue. A customer's total opens the **Aged Receivables Detail** for that customer on the same date and settings, and the last row's total opens it for everyone. On a phone each customer is a card.
- **A credit note, and a receipt or advance not yet applied to an invoice, are negative amounts in Current**, so a customer's total is their balance on Accounts Receivable. A receipt or credit note applied to an invoice has come off both: the invoice shows only what is still open on it, and the receipt no longer shows at all.
- What was applied counts only if it was effective on or before the date and has not been released, so the report for an earlier date shows the invoices as they stood then.
- The totals add up to the Accounts Receivable balance on the date, the same figure as the **Trial Balance**. A posting to Accounts Receivable with no contact, such as a manual journal, is its own row, **Not tied to a customer**, so nothing is left out.
- An invoice in another currency is shown in the branch's currency at the rate it was booked at.

The catalog's **Aged Receivables Summary**, used for export and scheduled emails, now needs **As at** and refuses a run without it. It gives the same figures, aged by due date in 30-day columns. The menu item had no report behind it before TK-222.

## Aged Receivables Detail

![Aged Receivables Detail](assets/screenshots/report-aged-receivables-detail.png)

The Aged Receivables Detail has its own page at `/reports/aged-receivables-detail` (TK-223), under **Reports › Sales Reports**. Besides access to reports it needs **sales.view**. It is the Aged Receivables Summary one document at a time: what makes up each customer's balance.

- **As at**, **Age by**, **Column width** and **Customer** are the summary's, and **As at** is required. The page opens as at today, or on the date, ageing and customer the summary's link opened it with.
- Each customer is a heading with its open documents under it, oldest first, and a **Total** for the customer. A document shows its number (a link, for an invoice, a credit note or a journal), its kind, its **Date**, its **Due** date (invoices only), **Days** overdue, and:
  - **Original**: what it came to before anything was applied to it;
  - **Settled**: what has been applied to it on or before the date, by a receipt, a credit note or an advance;
  - **Outstanding**: what is left, which is the amount placed in the one column its age gives, **Current** to the last overdue column.
- A credit note, or a receipt or advance not yet applied to an invoice, is negative, in **Current**, and has no days overdue. A document applied in full before the date is not listed; one applied after it is listed with what was open on the date.
- **Days** counts from the due date, or the invoice date when you age by that; an invoice not yet due has none. A document that is not an invoice, such as an opening balance or a journal, counts from the day it was posted.
- A customer's **Total** is that customer's row on the summary, and every column of the last **Total** row is the summary's total for that column; with one customer there is no last row, since it would repeat the customer's. Posting to Accounts Receivable with no contact, such as a manual journal, is listed under **Not tied to a customer**.
- The twelve columns scroll sideways on a laptop. On a phone each customer is a card with its documents inside, each showing the column its amount is in.

The catalog's **Aged Receivables Detail**, used for export and scheduled emails, now needs **As at** and refuses a run without it. It lists the same open documents, with the document's total and what is left of it (negative for a credit), aged by due date. The menu item had no report behind it before TK-223.

## Receivable Invoice Summary

![Receivable Invoice Summary](assets/screenshots/report-receivable-invoice-summary.png)

The Receivable Invoice Summary has its own page at `/reports/receivable-invoice-summary` (TK-224), under **Reports › Sales Reports**. Besides access to reports it needs **sales.view**. It lists the sales invoices raised in a period, newest first, with what each came to, what has been received and what is still due.

- **From** and **To** are both required, and are the **invoice date**. The page opens on the financial year to date and runs straight away. A **To** before **From** is refused under **To**. **Customer** and **Currency** narrow it, and **Include credit notes** adds them as negative amounts.
- **Received and due are as at the To date**, not as at today, so a report for a past period reads the same when you run it again. The page says so under the title.
- Each invoice is a row with its number (a link), **Date**, **Due** date (with how many days late when it is overdue), customer, currency, **Taxable**, **Tax** (CGST, SGST, IGST and cess added up), **Total**, **Received**, **Due** and **Status**. An invoice in another currency also shows its own total in smaller type under the base total. All the other figures are in the branch's base currency.
- **Received** is what receipts, and credit notes or advances applied to the invoice, settled on or before the To date. A receipt in a foreign currency is taken to base at the invoice's own rate. A settlement that was released, or is dated after the To date, is not counted.
- **Status tabs** above the list show how many documents have each status, whichever tab is open: **Awaiting payment** (nothing received, not yet due), **Partly paid** (something received, something owed, not yet due), **Overdue** (past its due date on the To date with something still owed, whether or not part of it has been received), **Paid** and **Void**. **All** is every status except Void. A voided invoice is listed only under **Void**, with nothing received or due, and a draft is never listed.
- A **credit note**, when included, shows negative amounts. Its **Received** is what has been applied to invoices and its **Due** what has not, so it is **Paid** once it is fully applied. It has no due date, so it is never overdue.
- The list loads 50 invoices at a time with **Show more invoices**. The **Total** row, the tab counts and the sentence above the list are of every invoice the filters match, not only those loaded.
- On a phone each invoice is a card.

The catalog's **Receivable Invoice Summary**, used for export and scheduled emails, now needs both **From** and **To** and refuses a run without them. It lists the posted invoices in the period with the total and what is due as at the To date; voided invoices and credit notes are not in it.

## Receivable Invoice Detail

![Receivable Invoice Detail](assets/screenshots/report-receivable-invoice-detail.png)

The Receivable Invoice Detail has its own page at `/reports/receivable-invoice-detail` (TK-225), under **Reports › Sales Reports**. Besides access to reports it needs **sales.view**. It is the Receivable Invoice Summary line by line: the sales invoices raised in a period, newest first, each with the items, quantities, prices, discounts and taxes it is made of.

- **From** and **To** are both required, and are the **invoice date**. The page opens on the financial year to date and runs straight away. A **To** before **From** is refused under **To**. **Customer**, **Item** and **Item category** narrow it. The **status tabs** above the list are the Receivable Invoice Summary's, with the same rules: **Awaiting payment**, **Partly paid**, **Overdue**, **Paid** and **Void**, and **All** is every status except Void. The statuses are as at the **To** date.
- Each invoice starts with a heading line: its number (a link), status, date, due date and customer. Under it, one row for each **line**: the item's name and code (a line typed in without an item shows its own description), **Qty** with its unit, **Unit price**, **Discount** (with the percentage when it was entered as one), **Taxable**, **Tax** (the line's tax rate under the amount) and **Total**. An **Invoice total** row closes it.
- A **Round off** row appears when the lines do not quite make the invoice total, the difference being the rupee rounding added to the invoice. The lines and the round off add up to the invoice total, and the invoice totals add up to the Receivable Invoice Summary's.
- An invoice's amounts are in **its own currency**. An invoice in another currency shows its code after each amount and its total in the branch's currency beside the invoice total. The **Total** row at the foot, and the sentence above the list, are in the branch's currency and are of every invoice the filters match, not only those loaded.
- **Item** and **Item category** narrow the lines as well as the invoices: only an invoice with a line for that item (or an item in that category) is listed, and only those lines are shown. The page says so above the list. The invoice total is then the total of the lines shown, no round off is shown, and the totals at the foot are of those lines.
- The list loads 25 invoices at a time with **Show more invoices**. On a phone each invoice is a card with its lines inside.

The catalog's **Receivable Invoice Detail**, used for export and scheduled emails, now needs both **From** and **To** and refuses a run without them. It lists a row for each line of the posted invoices in the period, with the line total in the branch's currency; voided invoices are not in it.

## Invoice Track Report

![Invoice Track Report](assets/screenshots/report-invoice-track.png)

The Invoice Track Report has its own page at `/reports/invoice-track-report` (TK-226), under **Reports › Sales Reports**. Besides access to reports it needs **sales.view**. It follows each sales invoice through its life: where it stands, the day it was posted, when it falls due, the last payment against it and what is still owed.

- **From** and **To** are both required, and are the **invoice date**. The page opens on the financial year to date and runs straight away. A **To** before **From** is refused under **To**. **Customer** narrows it.
- **Status tabs** count the invoices in each state, whichever tab is open: **Draft** and **Ready to post** (not in the books yet), then **Awaiting payment** (nothing received, not yet due), **Partly paid**, **Overdue** (past its due date on the To date with something still owed, whether or not part of it has been received), **Paid** and **Void**. **All** is every state except Void, which is listed only under its own tab.
- Each invoice is a row with its number (a link) and invoice date, customer, **Status**, **Posted** (the day it was posted, in your own time; blank until it is), **Due date** (with how many days late when it is overdue), **Last payment**, **Total** and **Due**. An invoice in another currency also shows its own total in smaller type under the total in the branch's currency.
- **Last payment** is the latest receipt, credit note or advance applied to the invoice on or before the To date. **Due** is what is left on that date, so a past period reads the same when you run it again. A settlement that was released, or is dated after the To date, is not counted.
- A **draft** or an invoice **ready to post** is not in the books, so it owes nothing and has no posting or payment dates. The **Total of posted** row at the foot adds up the posted invoices the filters match that still stand, so drafts and voided invoices are not in it, and it covers every match rather than only the rows loaded.
- The list loads 25 invoices at a time with **Show more invoices**. On a phone each invoice is a card. The product does not record when an invoice was sent, so there is no *sent* step.

The catalog's **Invoice Track**, used for export and scheduled emails, now needs both **From** and **To** and refuses a run without them. It lists the invoices of the period with where each stands, its total in the branch's currency and what is still due on the To date; voided invoices are not in it.

## Invoice/DN Payment Collection Report

![Invoice/DN Payment Collection Report](assets/screenshots/report-invoice-dn-payment-collection.png)

The Invoice/DN Payment Collection Report has its own page at `/reports/invoice-dn-payment-collection-report` (TK-227), under **Reports › Sales Reports**. Besides access to reports it needs **sales.view**. It shows what was collected in a period and which invoices each receipt settled.

- **From** and **To** are both required, and are the **receipt date**. The page opens on the financial year to date and runs straight away. A **To** before **From** is refused under **To**. **Customer**, **Bank account** and **Payment method** narrow it.
- Each posted receipt is a heading with its number (a link), date, bank account, payment method and reference, the customer, and the **Amount** that reached the bank. When the customer withheld **TDS**, it is shown under the amount. Under it are the invoices the receipt settled, each with its date, the day the settlement took effect and what was settled of it. A document with no number of its own, such as an opening balance, is shown by its code and number.
- A receipt's lines settle what reached the bank **and** any TDS withheld. What it did not settle against a document, an advance for instance, is shown on a **Not settled against a document** line, so the documents and that line always add up to the amount received plus the TDS.
- A receipt in **another currency** shows its amounts in that currency, with the amount in the branch's currency beneath it. **Total collected** at the foot, the sentence above the list and **Collected by customer** are in the branch's currency and are of every receipt the filters match, not only those loaded. The customers who paid most are listed (the ten largest; the others are added into one line).
- A **voided** receipt was taken back and a **draft** was never collected, so neither is listed. A credit note applied to an invoice is not a receipt and is not listed.
- The list loads 25 receipts at a time with **Show more receipts**. On a phone each receipt is a card with its documents inside. Sales has no debit note yet, so the documents shown are invoices, and anything else a receipt was allocated to.

The catalog's **Invoice/DN Payment Collection**, used for export and scheduled emails, now needs both **From** and **To** and refuses a run without them. It lists a row for each invoice a posted receipt of the period settled, with the amount settled in the branch's currency.

## Sales Analysis

![Sales Analysis](assets/screenshots/report-sales-analysis.png)

The Sales Analysis has its own page at `/reports/sales-analysis` (TK-228), under **Reports › Sales Reports**. Besides access to reports it needs **sales.view**. It adds up what sold in a period by item, item category or customer.

- **From** and **To** are both required, and are the **invoice date**. The page opens on the financial year to date, by item, and runs straight away. A **To** before **From** is refused under **To**. **Group by** chooses item, item category or customer. **Customer**, **Item category** and **Warehouse** narrow it.
- Sales are posted invoices and POS sales, **less posted credit notes**. **Include credit notes** is on, because a return is not a sale; turn it off to see sales as invoiced. A credit note that is a **sales return** takes its units off as well; a price correction or a discount agreed afterwards takes value off and no units.
- Each row shows **Qty** (in each item's base unit), **Gross** (quantity times price, as keyed, so a price that includes tax shows it here), **Discount**, **Net sales** (before tax, after discount), **Cost**, **Margin** and **Margin %**, all in the branch's base currency. A document in another currency is taken to base at the rate it was booked at. Rows run largest net sales first, and the total of everything the filters match is under them. A line with no item, such as a service typed in, is one row of its own.
- **Cost** is what the stock the sales moved cost, once costing has settled it. Costing runs just after a sale, so a line may not have its cost yet. Such a line is counted in the net sales, its cost is left out and marked **pending**, and **the row shows no margin until it is settled**: a margin worked out without that cost would be too high. A line with no stock movement, a service for instance, costs nothing and is not pending.
- **Compare with another period** starts with the period of the same length just before, and can be changed. It adds each row's **Net then** and the **Change** as a percentage of it, and the total's. A row with nothing sold in the other period shows no change.
- The page loads 50 rows at a time with **Show more**. On a phone each row is a card.

The catalog's **Sales Analysis**, used for export and scheduled emails, now needs both **From** and **To** and refuses a run without them. It lists each item sold with its units and gross value in the branch's currency, less credit notes.

## Sales Analysis Detail Report

![Sales Analysis Detail Report](assets/screenshots/report-sales-analysis-detail.png)

The Sales Analysis Detail Report has its own page at `/reports/sales-analysis-detail-report` (TK-229), under **Reports › Sales Reports**. Besides access to reports it needs **sales.view**. It is the Sales Analysis before it is added up: the lines behind each figure.

- **From** and **To** are both required, and are the **invoice date**. The page opens on the financial year to date and runs straight away. A **To** before **From** is refused under **To**. **Group by** chooses item, item category or customer. **Item**, **Item category**, **Customer** and **Warehouse** narrow it. **Include credit notes** is on, and lists a credit note's lines as negative amounts.
- Each group has a heading with its name and how many lines it has, then one row for each line: the **Date**, the **Document** (a link to the invoice or credit note), the customer (or the item, when grouped by customer), **Qty**, **Unit price**, **Discount**, **Net sales**, **Cost** and **Margin**. All amounts are in the branch's base currency; the unit price of a document in another currency is taken to base at the rate it was booked at.
- Each group ends with a **Subtotal** that adds up *every* line the group has, even those not yet loaded, and is the same figure as the group's row in the Sales Analysis for the same dates and filters. **Total** at the foot and the sentence above the list are of everything the filters match.
- A line whose **cost is pending** (costing has not settled the stock it moved yet) shows its cost as pending and **no margin**, and a group or total with such a line has no margin either, so a margin is never overstated. A line with no item is a group of its own.
- Lines are ordered by group name, then by date. The list loads 50 lines at a time with **Show more lines**; a group that runs over carries on under the same heading with its full subtotal. On a phone each group is a card with its lines inside.

The catalog's **Sales Analysis Detail**, used for export and scheduled emails, now needs both **From** and **To** and refuses a run without them. It lists a row for each line, credit note lines included as negatives, with the unit price and the line's net sales in the branch's currency.

## Sales Order Track Report

![Sales Order Track Report](assets/screenshots/report-sales-order-track.png)

The Sales Order Track Report has its own page at `/reports/sales-order-track-report` (TK-230), under **Reports › Sales Reports**. Besides access to reports it needs **sales.view**. It follows each sales order from the order to the invoice.

- **From** and **To** are both required, and are the **order date**. The page opens on the financial year to date and runs straight away. A **To** before **From** is refused under **To**. **Customer** narrows it, and **Outstanding only** keeps the orders with something still to deliver or to bill.
- **Progress tabs** count the orders at each stage, whichever tab is open: **Open** (nothing delivered), **Partly delivered**, **Delivered** (every line delivered in full, not every line billed), **Invoiced** (every line billed in full), **Closed short** (closed with something left undelivered, and not every line billed) and **Cancelled** (voided or cancelled). **All** is every progress except Cancelled, which is listed only under its own tab. Progress counts every line: one line that cannot be delivered, or billed, keeps the order from being delivered, or invoiced.
- Each order has a heading with its number (a link), progress, date, the date it was promised for, the customer and, when invoices have been raised from it, how many and what they came to. Under it, a row for each **line** with the item, **Ordered**, **Delivered**, **Invoiced**, **To deliver** (ordered less delivered) and **To bill** (ordered less invoiced), in the line's own unit, and the line's **Value**, the value **to deliver** and the value **to bill**. An **Order value** row closes it.
- **Values are before tax in the branch's currency, at the order's own price per unit**, so the outstanding values are the ordered value less what was delivered or invoiced and add up exactly. If a price was changed when the invoice was raised, that shows in what the invoices raised came to, not in the invoiced value.
- **Delivered** and **Invoiced** are the lines' running figures, which a delivery challan and an invoice raised from the order advance. The **Total** at the foot and the sentence above the list are of every order the filters match, not only those loaded.
- The list loads 25 orders at a time with **Show more orders**. On a phone each order is a card with its lines inside.

The catalog's **Sales Order Track**, used for export and scheduled emails, now needs both **From** and **To** and refuses a run without them. It lists the orders of the period but cancelled ones, with each one's progress and its value in the branch's currency.

## Quotation Track Report

![Quotation Track Report](assets/screenshots/report-quotation-track.png)

The Quotation Track Report has its own page at `/reports/quotation-track-report` (TK-231), under **Reports › Sales Reports**. Besides access to reports it needs **sales.view**. It shows what became of each quote you sent.

- **From** and **To** are both required, and are the **quote date**. The page opens on the financial year to date and runs straight away. A **To** before **From** is refused under **To**. **Customer** narrows it.
- **The strip** at the top counts the quotes **raised** and how many were **converted**, with their value in the branch's currency, and gives the **conversion rate by number** (converted over raised) and **by value**. It covers every quote the filters match, whichever tab is open, and a voided quote is counted in neither raised nor converted.
- **Status tabs** count the quotes at each outcome: **Open** (still valid, no answer), **Expired** (past its valid-until date, with no answer and no conversion), **Accepted** (the customer accepted it on the portal, and it has not been converted yet), **Converted to order**, **Converted to invoice**, **Declined** (the customer rejected it on the portal) and **Void**. **All** is every outcome except Void, which is listed only under its own tab.
- **Converted** means a posted sales order or invoice was raised from the quote, and it wins over what the customer answered. When both exist the invoice is named; with two of a kind, the earliest. **Converted to** shows that document's number (a link) and **Days to convert** counts from the quote date to its date. A draft or voided order or invoice does not convert a quote.
- **Status is as it stands today**, whatever the period: a conversion that happened after the period counts, and a quote is expired from the day after its valid-until date.
- Each row has the quote number (a link) and date, the customer, **Valid until**, the status, **Converted to**, **Days to convert** and the **Value** in the branch's currency (a quote in another currency also shows its own total under it). **Total** at the foot is of every quote in the tab chosen, not only those loaded.
- The list loads 25 quotes at a time with **Show more quotes**. On a phone each quote is a card.

The catalog's **Quotation Track**, used for export and scheduled emails, now needs both **From** and **To** and refuses a run without them. It lists the quotes of the period but void ones, with the **Valid until** date, the **Outcome** and the value in the branch's currency. Two columns are new, so a branch set up earlier gets them when its report catalog is seeded again.

## Delivery Order Track Report

![Delivery Order Track Report](assets/screenshots/report-delivery-order-track.png)

The Delivery Order Track Report has its own page at `/reports/delivery-order-track-report` (TK-232), under **Reports › Sales Reports**. Besides access to reports it needs **sales.view**. It follows each delivery challan from the goods leaving to the invoice.

- **From** and **To** are both required, and are the **challan date**. The page opens on the financial year to date and runs straight away. A **To** before **From** is refused under **To**. **Customer** and **Challan type** (sale, job work, approval, branch transfer or sample) narrow it, and **Not yet invoiced only** keeps the challans with something still to bill.
- **Status tabs** count the challans at each stage, whichever tab is open: **Not invoiced** (nothing on it billed), **Partly invoiced** (something billed and something still to bill, even if one line is billed in full) and **Invoiced** (every line billed in full), then **Void**. **All** is every status except Void, which is listed only under its own tab. A challan can be voided only as a draft, so a void challan delivered nothing.
- Each challan has a heading with its number (a link), status, type, date, the day it was dispatched, the customer, the **sales order it was delivered against** (a link) and the **invoices raised from it** (links). Under it, a row for each **line** with the item, **Delivered**, **Invoiced** (what invoices raised from the challan have billed) and **To bill** (delivered less invoiced), in the line's own unit, and the line's **Delivered value**, **Invoiced value** and **To bill value**. A **Challan value** row closes it.
- **Values are before tax in the branch's currency, at the challan's own price per unit**, so the invoiced and to-bill values are shares of the delivered value and add up exactly.
- **Invoiced is the line's own running figure**, which an invoice raised from the challan advances (and voiding it takes back). An invoice raised from the sales order that does not name the challan bills the order's lines and clears the goods-delivered-not-invoiced account, but does not advance the challan's figure, so the challan keeps reading as not invoiced; raise the invoice from the challan to keep the two in step.
- Only a sale challan is a supply. Job work, approval, branch transfer and sample challans move goods without a sale, so they stay unbilled unless an invoice names them (for an approval the customer keeps).
- The **Total** at the foot and the sentence above the list are of every challan the filters match, not only those loaded.
- The list loads 25 challans at a time with **Show more challans**. On a phone each challan is a card with its lines inside.

The catalog's **Delivery Challan Track**, used for export and scheduled emails, now needs both **From** and **To** and refuses a run without them. It lists the challans of the period but void ones, with the type, how far each has been billed, its value and what is still to bill in the branch's currency. Four columns are new, so a branch set up earlier gets them when its report catalog is seeded again.

## Aged Payables Summary

![Aged Payables Summary](assets/screenshots/report-aged-payables-summary.png)

The Aged Payables Summary has its own page at `/reports/aged-payables-summary` (TK-233), under **Reports › Purchase Report**. Besides access to reports it needs **purchase.view**. It is the Aged Receivables Summary the other way round: what the branch owed each vendor on a date and how overdue it was, in the branch's base currency.

- **As at** is required. The page opens as at today and runs straight away; a missing date is refused under the field.
- **Age by** counts a bill's age from its **due date** (the default) or its **bill date**. A document that is not a bill, such as an opening balance or a manual journal, counts from the day it was posted.
- **Column width** is 30 days (the default), 15 or 7. There are always five columns: **Current** (not yet due, or due on the date), then four overdue columns, the last being everything beyond three widths.
- **Vendor** narrows the report to one vendor. **Show vendors owed nothing** also lists the vendors with nothing open.
- Each vendor is a row with its code, the five columns and a **Total**, and a **Total** row closes the table. A sentence above says how much is owed and how much of it is overdue. A vendor's total opens the **Aged Payables Details** for that vendor on the same date and settings, and the last row's total opens it for everyone. On a phone each vendor is a card.
- **A debit note, and a payment or advance not yet applied to a bill, are negative amounts in Current**, so a vendor's total is what the branch owes them. A payment or debit note applied to a bill has come off both: the bill shows only what is still open on it, and the payment no longer shows at all. What was applied counts only if it was effective on or before the date and has not been released.
- The totals add up to the Accounts Payable balance on the date, the same figure as the **Trial Balance**. A posting to Accounts Payable with no contact, such as a manual journal, is its own row, **Not tied to a vendor**, so nothing is left out. A bill in another currency is shown in the branch's currency at the rate it was booked at, and a payment in that currency is taken to the branch's currency at the same rate, so it clears the bill exactly.
- **A vendor that is a micro or small enterprise is marked MSME.** The mark opens the **MSME ageing report**, the Sec 43B(h) view of the bills that should be paid within 45 days.

The catalog's **Aged Payables Summary**, used for export and scheduled emails, now needs **As at** and refuses a run without it. It gives the same figures, aged by due date in 30-day columns. The menu item had no report behind it before TK-233.

## Aged Payables Details

![Aged Payables Details](assets/screenshots/report-aged-payables-details.png)

The Aged Payables Details has its own page at `/reports/aged-payables-details` (TK-234), under **Reports › Purchase Report**. Besides access to reports it needs **purchase.view**. It is the Aged Payables Summary one document at a time: what makes up what the branch owes each vendor on a date.

- **As at** is required. The page opens as at today and runs straight away, or on the date, ageing, column width and vendor the summary linked it with; a missing date is refused under the field. **Age by**, **Column width** and **Vendor** are the summary's.
- Each vendor has a heading (an MSME is marked, and the mark opens the **MSME ageing report**), then every document still open on the date, oldest first: a **bill**, a **debit note**, a **payment** or a vendor's **receipt** not yet applied, an opening balance or a manual journal. A vendor with more than one document has a **Total** row.
- A document shows its **Date**, its **Due** date (only a bill has one), the **Days** it is overdue, what it came to (**Original**), what has been applied to it by the date (**Settled**) and what is left (**Outstanding**), and the outstanding amount sits in the one column its age gives. A bill, a debit note and a journal link to their page; a payment or receipt is only in its register.
- **A debit note, and a payment not yet applied to a bill, are negative amounts in Current.** A document applied in full on or before the date is not listed, and one applied after it is listed with what was open then.
- **A vendor's total is that vendor's row on the Aged Payables Summary**, in every column, and the **Total** row is the Accounts Payable balance on the date, because both read the same open documents. A posting to Accounts Payable with no contact, such as a manual journal, is listed under **Not tied to a vendor**. On a phone each vendor is a card with its documents inside.

The catalog's **Aged Payables Details**, used for export and scheduled emails, now needs **As at** and refuses a run without it (it used to treat the date as optional and listed every bill at its full total). It lists a row for each open document, with its total and what is left, and a debit note or an unapplied payment shows as negative.

## Payable Invoice Summary

![Payable Invoice Summary](assets/screenshots/report-payable-invoice-summary.png)

The Payable Invoice Summary has its own page at `/reports/payable-invoice-summary` (TK-235), under **Reports › Purchase Report**. Besides access to reports it needs **purchase.view**. It is the Receivable Invoice Summary the other way round: every vendor bill in a period with its tax and total, what has been paid and what is still due.

- **From** and **To** are both required, and are the **bill date**. The page opens on the financial year to date and runs straight away. A **To** before **From** is refused under **To**. **Vendor** and **Currency** narrow it, and **Include debit notes** lists debit notes too, as negative amounts.
- **Status tabs** count the documents at each status, whichever tab is open: **Awaiting payment** (nothing paid, not yet due), **Partly paid**, **Overdue** (past its due date on the To date with something still owed, which wins over partly paid), **Paid** and **Void**. **All** is every status except Void, which is listed only under its own tab. Drafts are never listed.
- Each bill is a row with our number (a link) and the vendor's own bill number under it, its date, due date (with how many days late when it is overdue), the vendor, the currency, **Taxable**, **Tax**, **Total**, **Paid**, **Due** and the status. A bill in another currency shows its own total under the total in the branch's currency.
- **Paid and due are as at the To date**, so a report for a past period reads the same when it is run again. Paid is what payments, and debit notes or advances applied to the bill, settled on or before that date; a payment in the vendor's currency is taken to the branch's currency at the bill's own rate. A settlement that was released, or takes effect after the To date, does not count.
- **What the vendor is owed is not always the total.** **TDS withheld** is not paid to the vendor, and on a **reverse-charge** bill the tax is the branch's own to pay, so the row shows **owed** under the total where it differs, and **Due** is what is owed less what was paid. Where there is neither, due is the total less paid.
- A **debit note**, when included, is negative: its **Paid** is what has been applied to bills and its **Due** what has not yet been.
- The **Total** row and the sentence above the list are of every document the filters match, not only those loaded. The taxable total is the sum of the posted bills' taxable amounts in the branch's currency, which is what the purchase register adds up for the same bills.
- The list loads 50 at a time with **Show more bills**. On a phone each bill is a card.

The catalog's **Payable Invoice Summary**, used for export and scheduled emails, now needs both **From** and **To** and refuses a run without them. It lists the posted bills of the period with their total in the branch's currency and what is still due on the **To** date; voided bills and debit notes are not listed.

## Payable Invoice Detail

![Payable Invoice Detail](assets/screenshots/report-payable-invoice-detail.png)

The Payable Invoice Detail has its own page at `/reports/payable-invoice-detail` (TK-236), under **Reports › Purchase Report**. Besides access to reports it needs **purchase.view**. It is the Payable Invoice Summary line by line: every vendor bill in a period with the items, quantities, prices, discounts and taxes it is made of.

- **From** and **To** are both required, and are the **bill date**. The page opens on the financial year to date and runs straight away. A **To** before **From** is refused under **To**. **Vendor**, **Item** and **Item category** narrow it.
- **The bills are the summary's**: the same period, the same statuses and the same tab counts (**Awaiting payment**, **Partly paid**, **Overdue**, **Paid** and **Void**; All is every status except Void). A bill is the heading of its lines, with its number (a link), the vendor's own bill number, its dates, the vendor and its status; a **Reverse charge** bill says so. Debit notes are not listed here, and drafts never are.
- Each line shows the item with its code, the description when it says more than the name, the quantity and unit, the unit price, any discount, the taxable value and the tax with its rate, and the line total. A line that is not for an item, such as freight or a service, shows its own description. **A line bought as a fixed asset is marked** with its asset category, such as **Fixed asset · Plant and machinery**.
- **Lines and a bill's total are in the bill's own currency**, with its total in the branch's currency beside it on a foreign bill. **Round off**, what the rupee rounding added, is given as a row of its own, so the lines and the round off make the bill's total, the same figure the summary shows.
- **Choosing an item or a category narrows the lines as well as the bills.** Only a bill with such a line is listed, and only those lines, so a bill's total below is of those lines and not of the whole bill: no round off is shown, and the page says so. The status tabs count only those bills.
- The **Total** row and the sentence above the list are in the branch's currency, of every bill the filters match and not only those loaded; each line is taken to the branch's currency at its bill's rate. For the same bills, with nothing narrowing the lines, the taxable and tax totals equal the Payable Invoice Summary's.
- The list loads 25 bills at a time with **Show more bills**. On a phone each bill is a card with its lines.

The catalog's **Payable Invoice Detail**, used for export and scheduled emails, now needs both **From** and **To** and refuses a run without them. It has a row for each line of each posted bill of the period, with the line total in the branch's currency; voided bills and drafts are not listed.

## Bills Track Report

![Bills Track Report](assets/screenshots/report-bills-track.png)

The Bills Track Report has its own page at `/reports/bills-track-report` (TK-237), under **Reports › Purchase Report**. Besides access to reports it needs **purchase.view**. It follows each vendor bill through its life: where it is stuck or standing, the dates of its steps and what is still owed to the vendor.

- **From** and **To** are both required, and are the **bill date**. The page opens on the financial year to date and runs straight away. A **To** before **From** is refused under **To**. **Vendor** narrows it.
- **Status tabs** count the bills at each step, whichever tab is open: **Draft**, **Ready to post**, **Awaiting payment**, **Partly paid**, **Overdue**, **Paid** and **Void**. **All** is every status except Void, which is listed only under its own tab. Drafts and bills ready to post are listed, because that is where a bill gets stuck; they are not in the books, so they have no due date and nothing is owed on them.
- Each bill is a row with its number (a link), its date and the vendor's own bill number, the vendor, its status, the day it was **Posted** (in your own time), its **Due date** (with how many days late when it is overdue), the **Last payment**, the **Total** and what is **Due**. A **Reverse charge** bill says so.
- **Approval** is shown under the status for a bill that went through an approval workflow: **Approved**, **Rejected**, or **In approval** with the level it waits on. A bill that was never sent through a workflow shows nothing. An approved bill is still a draft or ready to post until it is posted.
- **Paid and due are as at the To date**, so a report for a past period reads the same when it is run again. The last payment is the latest payment, or debit note or advance applied to the bill, on or before that date; one that was released, or takes effect after the To date, does not count.
- **What is due is what is owed to the vendor, which is not always the total.** TDS withheld is not paid to the vendor, and on a reverse-charge bill the tax is the branch's own to pay, so where that differs from the total the row shows **owed** under it. Due is the same figure as on the Payable Invoice Summary for the same bill.
- The **Total of posted** row and the sentence above the list add up the posted bills that still stand, of every bill the filters match and not only those loaded; drafts, bills ready to post and voided bills are not in it. A bill in another currency shows its total in the branch's currency, and its own total under it.
- The list loads 25 bills at a time with **Show more bills**. On a phone each bill is a card.

The catalog's **Bills Track**, used for export and scheduled emails, now needs both **From** and **To** and refuses a run without them. It lists each bill of the period but a voided one, with where it stands, its total in the branch's currency and what is still owed on the **To** date; a draft shows nothing owed.

## Bill/DN Payment Report

![Bill/DN Payment Report](assets/screenshots/report-bill-dn-payment.png)

The Bill/DN Payment Report has its own page at `/reports/bill-dn-payment-report` (TK-238), under **Reports › Purchase Report**. Besides access to reports it needs **purchase.view**. It shows what was paid to vendors in a period and which bills each payment settled.

- **From** and **To** are both required, and are the **payment date**. The page opens on the financial year to date and runs straight away. A **To** before **From** is refused under **To**. **Vendor**, **Bank account** and **Payment method** narrow it.
- Each posted **Spend money** payment is a heading with its number (a link), date, the bank account it left, how it was paid (with the cheque or transaction reference), the vendor and the amount, and under it the **bills it settled** with each bill's number (a link), date, the vendor's own bill number, the day the settlement took effect and the amount settled. A payment that settled something that is not a bill, such as an opening balance, lists it by its own code and number.
- **What a payment did not settle** is its own line, **Not settled against a document**: an advance paid before the bill exists, or more than the bill came to. A payment from which **TDS was withheld** shows the TDS under its amount; the TDS is settled along with what left the bank, so a payment of 4,500 with 500 withheld that settles a 5,000 balance has nothing left over. For every payment, what it settled and what it did not make up the amount and the TDS.
- A payment is in its own currency: a dollar payment shows its amounts in dollars, with its amount in the branch's currency under it. **Total paid**, at the foot, and **Paid by vendor**, above the list (the ten paid most, and the rest as one line), are in the branch's currency, of every payment the filters match and not only those loaded.
- A **draft** was never paid, a **voided** payment was taken back, and a **TDS challan**, which deposits deducted TDS with the government, is not a payment to a vendor: none of those is listed. A debit note applied to a bill is not a payment either, so it is not listed here. The total paid equals the posted Spend money to vendors for the same period.
- The list loads 25 payments at a time with **Show more payments**. On a phone each payment is a card with the bills it settled.

The catalog's **Bill / DN Payment**, used for export and scheduled emails, now needs both **From** and **To** and refuses a run without them. It has a row for each bill a posted payment of the period settled, with the amount settled in the branch's currency.

## Purchase Analysis

![Purchase Analysis](assets/screenshots/report-purchase-analysis.png)

The Purchase Analysis has its own page at `/reports/purchase-analysis` (TK-239), under **Reports › Purchase Report**. Besides access to reports it needs **purchase.view**. It shows what was bought in a period, added up by item, item category or vendor.

- **From** and **To** are both required, and are the **bill date**. The page opens on the financial year to date, by item, and runs straight away. A **To** before **From** is refused under **To**. **Group by** chooses item, item category or vendor; **Vendor** and **Item category** narrow it.
- The list is posted **bills** less posted **debit notes**, largest net purchases first. A debit note takes units off only when it is a purchase return; a price correction or a discount takes value off and no units. Turn **Include debit notes** off to see what was bought before anything went back.
- **Fixed assets bought on a bill are left out**, since they are not stock; tick **Include fixed assets** to count them. A line with no item, such as freight or a service, is a group of its own.
- Each group shows its **Qty** (in each item's own unit), **Gross** (quantity times price as keyed, so a price that includes tax shows it there), **Discount**, **Net purchases** (before tax, after discount), the **Avg cost** of a unit and its **Share** of all net purchases. The average cost is shown for an item when adding up by item, where a unit means one thing; it is blank for a category or a vendor, and for the line with no item. The **Total** row and the sentence above the list are of every group the filters match, not only those loaded.
- Tick **Compare with another period** to add each group's **Net then** and the **Change** in net purchases against it; the second period starts as the period just before this one, and both of its dates are needed. A group with nothing bought then has no change.
- Amounts are in the branch's currency, each line taken at its bill's own rate. Net purchases of stock items, with debit notes unticked, equal what the bills posted to **Inventory** for the same dates, apart from goods received before they were billed (the receipt posted those) and GST that cannot be claimed (the ledger adds it to the stock's cost; it is not here).
- The list loads 50 groups at a time with **Show more**. On a phone each group is a card.

The catalog's **Purchase Analysis**, used for export and scheduled emails, now needs both **From** and **To** and refuses a run without them. It has a row for each item bought, with the units and the gross value in the branch's currency less debit notes; a line with no item and a fixed asset are left out.

## Purchase Order Track Report

![Purchase Order Track Report](assets/screenshots/report-purchase-order-track.png)

The Purchase Order Track Report has its own page at `/reports/purchase-order-track-report` (TK-240), under **Reports › Purchase Report**. Besides access to reports it needs **purchase.view**. It follows each purchase order from the order to the receipt and the bill.

- **From** and **To** are both required, and are the **order date**. The page opens on the financial year to date and runs straight away. A **To** before **From** is refused under **To**. **Vendor** narrows it, and **Outstanding only** keeps the orders with something still to receive or to bill.
- **Progress tabs** count the orders at each stage, whichever tab is open: **Open** (nothing received), **Partly received**, **Received** (every line received in full, not every line billed), **Billed** (every line billed in full), **Closed short** (closed with something never received, and not every line billed) and **Cancelled** (voided or withdrawn). **All** is every progress except Cancelled, which is listed only under its own tab. Progress counts every line: one line that has not arrived, or not been billed, keeps the order from being received, or billed.
- Each order has a heading with its number (a link), progress, date, the date the vendor is expected to deliver, the vendor and, when bills have been raised against it, how many and what they came to. Under it, a row for each **line** with the item, **Ordered**, **Received**, **Billed**, **To receive** (ordered less received) and **To bill** (ordered less billed), in the line's own unit, and the line's **Value**, the value **to receive** and the value **to bill**. An **Order value** row closes it.
- **Values are before tax in the branch's currency, at the order's own price per unit**, so the outstanding values are the ordered value less what was received or billed and add up exactly. If the vendor billed a different price, that shows in what the bills came to, not in the billed value.
- **Received** is the line's running figure, which each posted goods receipt against the order advances by what it **accepted**; goods rejected at the door never arrived. **Billed** is added up from posted bills whose lines name the order's line, directly or through a goods receipt. A draft or voided bill bills nothing, a bill with no order behind it counts toward no order, and a debit note is not taken off.
- The **Total** at the foot and the sentence above the list are of every order the filters match, not only those loaded.
- The list loads 25 orders at a time with **Show more orders**. On a phone each order is a card with its lines inside.

The catalog's **Purchase Order Track**, used for export and scheduled emails, now needs both **From** and **To** and refuses a run without them. It lists the orders of the period but cancelled ones; its **Fulfilment status** column is the order's progress, and its total is in the branch's currency.

## Purchase Receive Order Details

![Purchase Receive Order Details](assets/screenshots/report-purchase-receive-order-details.png)

The Purchase Receive Order Details has its own page at `/reports/purchase-receive-order-details` (TK-241), under **Reports › Purchase Report**. Besides access to reports it needs **purchase.view**. It lists every goods receipt line of a period: what arrived, from whom, against which purchase order, into which warehouse and at what cost.

- **From** and **To** are both required, and are the **receipt date**. The page opens on the financial year to date and runs straight away. A **To** before **From** is refused under **To**. **Vendor**, **Item**, **Warehouse** and **Purchase order** narrow it; the purchase order list offers the newest orders a receipt was raised against.
- Only **posted** receipts are listed: a draft has received nothing, and a posted receipt cannot be voided. **Item** and **Warehouse** narrow the lines as well as the receipts, so only a receipt with such a line is listed, and only those lines; the page says so, and each receipt's value is then of the lines shown.
- Each receipt has a heading with its number (a link), date, vendor, the purchase order it was raised against (a link), the vendor's own delivery note, and its currency when it is not the branch's. Under it, a row for each **line** with the item, the **warehouse** it went into, **Delivered**, **Rejected**, **Accepted**, **Billed**, the **Unit cost** and the **Value**. A line with a batch shows its batch number and expiry, one with serial numbers lists them (the first fifty, with how many more), and a rejected line shows why. A **Receipt value** row closes it.
- **Accepted** is what went into stock: it is the quantity of the stock-in movement the receipt made, so each receipt's accepted quantities equal its stock movements. **Rejected** goods were refused at the door and are not owed for. **Billed** is what posted bills naming the line add up to; a draft or voided bill bills nothing.
- **Unit cost** is the line's value before tax over what was **delivered**, so rejecting goods does not raise the cost of the rest. **Value** is that cost times what was accepted, rounded to two places, which is what the receipt debits to **Inventory** and credits to Goods Received Not Invoiced.
- A receipt in another currency shows its lines and value in that currency with the value in the branch's currency under it. The **Total** at the foot and the sentence above the list are in the branch's currency and are of every receipt the filters match, not only those loaded.
- The list loads 25 receipts at a time with **Show more receipts**. On a phone each receipt is a card with its lines inside.

The catalog's **Purchase Receive Order Details**, used for export and scheduled emails, now needs both **From** and **To** and refuses a run without them. It has a row for each receipt line, with the quantity delivered, accepted and rejected and the value of the accepted goods in the branch's currency. Four columns are new (accepted quantity, rejected quantity, value and the line), so a branch set up earlier gets them when its report catalog is seeded again.

## Receive Order Track Report

![Receive Order Track Report](assets/screenshots/report-receive-order-track.png)

The Receive Order Track Report has its own page at `/reports/receive-order-track-report` (TK-242), under **Reports › Purchase Report**. Besides access to reports it needs **purchase.view**. It follows each goods receipt from the goods to the bill.

- **From** and **To** are both required, and are the **receipt date**. The page opens on the financial year to date and runs straight away. A **To** before **From** is refused under **To**. **Vendor** narrows it, and **Not yet billed only** keeps the receipts with something still to bill.
- **Status tabs** count the receipts at each stage, whichever tab is open: **To bill** (something was accepted and none of it is billed), **Partly billed** (something is billed, but not every line in full), **Billed** (every line with goods accepted is billed in full) and **Nothing to bill** (every line was rejected, so nothing was taken into stock and nothing is owed). **All** is every status. Status counts every line: one line not billed in full keeps the receipt from being billed.
- Each receipt has a row with its number (a link), date and the vendor's delivery note, the vendor, the purchase order it was raised against (a link), its **Value**, what is **Billed**, what is **To bill** and its status. A receipt in another currency shows its value in that currency under the branch's currency.
- **Value** is what the accepted goods are worth: the line's value before tax over what was **delivered**, times what was **accepted**, rounded to two places. That is what the receipt debits to **Inventory**, and goods rejected at the door are not in it. **Billed** is the share of it that posted bills naming the receipt's lines add up to, at the receipt's own cost (Purchase refuses a bill line at a different price), so **To bill** is value less billed and the figures add up exactly. A draft or voided bill bills nothing.
- Only **posted** receipts are listed: a draft has received nothing, and a posted receipt cannot be voided. The **Total** at the foot and the sentence above the list are of every receipt the filters match, not only those loaded.
- The list loads 25 receipts at a time with **Show more receipts**. On a phone each receipt is a card.

The catalog's **Receive Order Track**, used for export and scheduled emails, now needs both **From** and **To** and refuses a run without them. It has a row for each posted receipt of the period, its **Status** now the billing status above, with the value, the billed value and what is to bill in the branch's currency. Three columns are new, so a branch set up earlier gets them when its report catalog is seeded again.

## Inventory Item Summary

![Inventory Item Summary](assets/screenshots/report-inventory-item-summary.png)

The Inventory Item Summary has its own page at `/reports/inventory-item-summary` (TK-243), under **Reports › Inventory Report**. Besides access to reports it needs **inventory.view**. For each stocked item it shows what the period did to the stock.

- **From** and **To** are both required. The page opens on the financial year to date and runs straight away. A **To** before **From** is refused under **To**. **Item category** and **Item** narrow it, and **Show items with no movement** lists every stocked item; without it an item is listed only if it had stock at the start of **From** or moved in the period.
- Each item has a row with its name, code, unit, costing method and category, and in each column a **quantity over its value**: **Opening** (the stock at the start of the From date), **Purchased** (goods received less goods sent back to vendors), **Sold** (goods issued to customers less goods they returned, with the sales value, the net sales before tax, under it), **Adjusted** (every other movement, signed: stock counts, opening entries and transfers) and **Closing** (the stock at the end of the To date). Then **COGS** (what the goods sold cost) and **Profit** (sales less COGS).
- Opening plus purchased less sold plus adjusted is the closing quantity, and the closing value, of every item. Stock is one pool for the branch, so nothing is split by warehouse.
- Values come from the stock movements once they are **costed**. Costing runs just after a sale or purchase is saved, so an item with a movement that is not costed yet is marked **Cost pending**: its values are short by what that movement will cost, and its **Profit** is left blank (a dash) when what was sold is among them, since it would be overstated. The same goes for the total.
- The **Total** at the foot is of values only, since quantities in different units do not add; it and the sentence above the list are of every item the filters match, not only those loaded. With every movement costed, the **closing values add up to the balance of the Inventory account** at the To date.
- The list loads 25 items at a time with **Show more items**. On a phone each item is a card.

The catalog's **Inventory Item Summary**, used for export and scheduled emails, now works (it failed before) and needs both **From** and **To**, refusing a run without them. It has a row for each item that had stock at the start or moved in the period, with the same figures as the page and the accounts the item's sub-accounts are under.

## Inventory Item Detail

The Inventory Item Detail has its own page at `/reports/inventory-item-detail` (TK-244), under **Reports › Inventory Report**. Besides access to reports it needs **inventory.view**. It serves as the full stock card for stocked items, showing chronologically every movement that occurred during the chosen period.

- **From** and **To** are both required (D-67). The page opens on the current financial year to date and runs automatically. A **To** date earlier than **From** is refused with inline validation.
- **Filters:** Optional **Item**, **Item category**, **Warehouse**, and **Source** (Goods Receipt, Invoice, Sales Return, Purchase Return, Stock Adjustment, Transfer). Choosing an item category filters the item picker to items in that category.
- **Item Cards & Movements:** Each item is grouped with its item code, unit of measure, costing method, and category. The item ledger begins with an **Opening Balance** row (quantity and value prior to the From date), followed chronologically by each stock movement:
  - **Date** and **Document** number with direct source links where available.
  - **Source** document type and party/contact name.
  - **Warehouse** where the stock entered or departed.
  - **Qty In** or **Qty Out** and the transaction's **Unit Cost**.
  - **Value** of the movement (signed: incoming positive, outgoing negative).
  - **Sale Price** and **Margin** (both value and percentage) for customer sales and returns.
  - **Running Quantity** and **Running Value** updated movement-by-movement.
  - **Closing Balance** row showing period net changes and final closing quantity and value.
- **Reconciliation:** The closing quantity and closing value match the Inventory Item Summary (TK-243) closing balance for the same item and period.
- **Catalog Source:** The catalog's **Inventory Item Detail** report (`item-detail`) enforces required From and To dates and enriches document headers and party details.

## Inventory Item List

The Inventory Item List has its own page at `/reports/inventory-item-list` (TK-245), under **Reports › Inventory Report**. Besides access to reports it needs **inventory.view**. It shows every stocked and non-stocked item as at a chosen date, with its quantity on hand, average cost, FIFO cost layer, total inventory value, open purchase orders (on order), open sales orders (committed), and available quantity.

- **As at** date is required (D-67). The page defaults to today's date.
- **Filters:** Optional **Category**, **Status** (Active, Inactive, All), **Item Type** (Inventory Item, Non-Inventory Item, Service, All), **Search** (matching item code or name), and **Only items with stock** (when ticked, filters to items with non-zero stock).
- **Columns:** Code, Name, Category, Type, Unit, Status, On Hand, Average Cost, FIFO Cost, Total Value, On Order, Committed, and Available.
- **Arithmetic:** On Hand as at the date is derived from stock movements up to the date (not the current live `ItemStock`). Total value matches the closing balance in the Inventory Item Summary (TK-243) at the same date.
- **Totals:** The totals row at the bottom sums On Hand, Total Value, On Order, Committed, and Available quantities across all matched items. On a phone, each item is presented as a card.
- **Catalog Source:** The catalog's **Inventory Item List** report (`inventory-item-list`) enforces the required `asOf` date parameter, joins movements up to that date, and supplies explicit numeric decimal types to prevent SQL sum type conflicts.

## Inventory Aging Report

The Inventory Aging Report has its own page at `/reports/inventory-aging-report` (TK-246), under **Reports › Inventory Report**. Besides access to reports it needs **inventory.view**. It ages inventory held on hand into 0–30, 31–60, 61–90, 91–180, and 180+ day buckets from cost layer receipt dates up to a chosen as-at date.

- **As at** date is required (D-67). The page defaults to today's date.
- **Filters:** Optional **Category**, **Warehouse**, **Search** (item code or name), and **Only items with stock** (when ticked, filters to items with remaining stock).
- **Display Metric Toggle:** Allows switching instantly between **Valuation (Amount)** and **Stock Quantity** to evaluate inventory value or physical units.
- **Summary Metrics:** At the top of the report, metric cards display Total Stock and each of the 5 aging buckets with both units and valuation in the branch currency.
- **Arithmetic & FIFO Consistency:** Ages directly from remaining quantities of inbound cost layers (`CostLayer`), ensuring true FIFO-correct aging even for weighted-average items. The total inventory valuation at the as-at date matches the Inventory Item List (TK-245) and Inventory Item Summary (TK-243) at the same date.
- **Catalog Source:** The catalog's **Inventory Aging Report** (`inventory-aging`) marks the `asOf` date parameter as required (`IsRequired = true`).

## Batch Tracking Status Report

The Batch Tracking Status Report has its own page at `/reports/batch-tracking-status-report` (TK-247), under **Reports › Inventory Report**. Besides access to reports it needs **inventory.view**. It shows every batch created on or before a chosen as-at date, with its item details, manufacture and expiry dates, received and available quantities, and warehouse location.

- **As at** date is required (D-67). The page defaults to today's date.
- **Filters:** Optional **Item**, **Warehouse**, **Expiry Window** (30 days, 60 days, 90 days, 180 days, or Expired), **Search** (matching batch number, item code, or item name), **With stock only** (defaults checked, hides exhausted batches), and **Expired only**.
- **Summary Metrics:** At the top of the report, metric cards display **Total Received Quantity**, **Total Available Quantity**, **Near Expiry Batches** (expiring within 30 days, marked with warning highlighting), and **Expired Batches** (past expiry date, marked with danger highlighting).
- **Columns & Badges:** Batch No, Item Code, Item Name, Category, Warehouse, Mfg Date, Expiry Date (with dynamic warning/expired tags and days to expiry), Received Quantity, Available Quantity, and MRP in branch currency.
- **Arithmetic:** Available quantity is calculated from cumulative movements up to the as-at date. Matches the closing balance of batches in the Batch Tracking Detail Report (TK-248).
- **Catalog Source:** The catalog's **Batch Tracking Status** report (`batch-tracking-status`) marks the `asOf` date parameter as required (`IsRequired = true`), pre-groups movements to avoid correlated subquery limitations, and lists batches in First-Expired-First-Out (FEFO) order.

## Batch Tracking Detail Report

The Batch Tracking Detail Report has its own page at `/reports/batch-tracking-detail-report` (TK-248), under **Reports › Inventory Report**. Besides access to reports it needs **inventory.view**. It shows every movement of each batch during a chosen period, providing complete lot audit trails and traceability for recalls.

- **From** and **To** dates are both required (D-67). The page opens on the current financial year to date. A To date earlier than From date is refused with inline validation.
- **Filters:** Optional **Batch**, **Item**, **Warehouse**, and **Party / Contact**.
- **Summary Metrics:** At the top of the report, metric cards display **Total Batches**, **Total Opening Qty**, **Total In Qty**, **Total Out Qty**, and **Total Closing Qty**.
- **Grouped Layout:** Batches are listed as individual sections with batch number, item code and name, category, costing method, warehouse, and expiry date. Each batch header features a summary pill strip showing **Opening Balance**, **Total In**, **Total Out**, and **Closing Balance**.
- **Movements Grid:** Under each batch, movements are listed chronologically: Date, Document number, Movement Type, Party / Contact name, Warehouse, Qty In, Qty Out, and Running Balance.
- **Arithmetic:** Each batch's closing balance equals opening quantity plus total in minus total out, and matches its available quantity on the Batch Tracking Status Report (TK-247) at the To date.
- **Catalog Source:** The catalog's **Batch Tracking Detail Report** (`batch-tracking-detail`) marks both From and To parameters as required (`IsRequired = true`).

## Serial Tracking Status Report

The Serial Tracking Status Report has its own page at `/reports/serial-tracking-status-report` (TK-249), under **Reports > Inventory Report**. Besides access to reports it needs **inventory.view**. It shows every tracked serial number created on or before a chosen as-at date, with its current status, item details, warehouse, and receipt/issue documents.

- **As at** date is required (D-67). The page defaults to today's date.
- **Filters:** Optional **Item**, **Warehouse**, **Status** (All, In Stock, Issued, Returned, Damaged, Expired), and **Search** (matching serial number, item code, or item name).
- **Summary Metrics:** At the top of the report, metric cards display **Total Serials**, **In Stock**, **Issued**, and **Other Status** (Damaged/Expired/Returned).
- **Columns & Badges:** Serial Number, Item Code, Item Name, Category, Warehouse, Received Date, GRN / Receipt Ref, Status (with dynamic color badges), Issued Date, and Sale / Issue Ref.
- **Status Evaluation:** Effective status is evaluated as at the chosen As-At date: an item issued after the As-At date is accurately reported as In Stock as of that date.
- **Catalog Source:** The catalog's **Serial Tracking Status** report (`serial-tracking-status`) marks the `asOf` date parameter as required (`IsRequired = true`), joining `ReceivedMovement` and `IssuedMovement` to provide document references.

## Serial Tracking Detail Report

The Serial Tracking Detail Report has its own page at `/reports/serial-tracking-detail-report` (TK-250), under **Reports > Inventory Report**. Besides access to reports it needs **inventory.view**. It shows every movement of each serial number over a chosen period, providing full audit trails for warranty, recall, and specific identification costing.

- **From** and **To** dates are both required (D-67). The page opens on the current financial year to date. A To date earlier than From date is refused with an inline error under To.
- **Filters:** Optional **Serial Number**, **Item**, **Warehouse**, **Party / Contact**, and **Search** (matching serial number or item code/name).
- **Summary Metrics:** At the top of the report, metric cards display **Total Serials**, **Total Opening Qty**, **Total In Qty**, **Total Out Qty**, and **Total Closing Qty**.
- **Grouped Layout:** Serial pieces are listed as individual cards with serial number, item code and name, category, HUID hallmark, costing method, warehouse, and status badge. Each serial header features a summary pill strip showing **Opening**, **Total In**, **Total Out**, and **Closing Balance**.
- **Movements Grid:** Under each serial piece, movements are listed chronologically: Date, Document number, Movement Type, Party / Contact name, Warehouse, Qty In, Qty Out, Running Balance, and Notes.
- **Arithmetic:** Each serial's closing balance equals opening quantity plus total in minus total out, and matches its status on the Serial Tracking Status Report (TK-249) at the To date (1 = InStock, 0 = Sold/Issued).
- **Catalog Source:** The catalog's **Serial Tracking Detail Report** (`serial-tracking-detail`) marks both From and To parameters as required (`IsRequired = true`).

## Warehouse Tracking Status Report

The Warehouse Tracking Status Report has its own page at `/reports/warehouse-tracking-status-report` (TK-251), under **Reports > Inventory Report**. Besides access to reports it needs **inventory.view**. It shows warehouse-by-warehouse stock quantities received, issued, and available as at a chosen date, along with branch valuation estimates.

- **As at** date is required (D-67). The page opens on today.
- **Filters:** Optional **Warehouse**, **Category**, **Search** (matches warehouse name, code, item code, or item name), and an **Only Warehouses with Stock** toggle.
- **Summary Metrics:** At the top of the report, metric cards display **Total Warehouses**, **Stocked Warehouses**, **Total Items Stocked**, **Total Available Qty**, and **Total Estimated Value**.
- **Grouped Layout:** Warehouses are presented as structured cards displaying warehouse name, code, address, state, country, primary indicator, and active status badge.
- **Stock Grid:** Within each warehouse card, stocked items are tabulated: Item Code, Item Name, Category, UOM, Qty Received, Qty Issued, Available Qty, Average Unit Cost (branch level), and Estimated Stock Value.
- **Valuation Rule:** Warehouses are location dimensions only; the branch's stock pool and valuation are not split per warehouse. Valuation is computed as available quantity multiplied by the branch average unit cost.
- **Detail View:** An inline toggle provides quick access to movement details for the warehouse.
- **Catalog Source:** The catalog's **Warehouse Tracking Status** report (`warehouse-tracking-status`) marks the `asOf` date parameter as required (`IsRequired = true`).

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



