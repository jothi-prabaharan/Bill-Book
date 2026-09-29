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

## Business Performance

**Business Performance**, under **Accounting**, shows eight ratios for a period, one per row:

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

Each row shows its **Unit** — percent, days, times or an amount. Add the **Numerator** and **Denominator** columns to see the two figures behind a ratio, so you can check it against the Profit & Loss and the Balance Sheet.

With no dates, the report covers the twelve months to today. The ratios always appear in the order above and the report cannot be re-sorted.

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

Three reports compare the branch's budget with what was posted, under **Accounting** in the list and **Reports › Financial performance** in the menu. Each takes an **As at** date, today when left blank. Each also takes a **Budget**; when blank it uses the branch's approved budget for the year holding that date. With no budget, the reports still show the actuals.

- **Budget vs Actual**: each income and expense account's budget and actual, with the variance, the variance as a percentage of the budget, and whether it is **Favourable** or **Adverse**. **Period** chooses the date's month, its financial quarter, or the year to date (`month`, `quarter` or `ytd`; year to date when blank). The period always covers whole months, because a budget is monthly.
- **Budget vs Actual by Project**: the same for each project and account, for a budget kept by project.
- **Budget Monthly Trend**: one row per account and month of the budget's year, with budget and actual side by side. Pivot on **Month** to lay the twelve months across.

How the figures are worked out:

- **Actual** is exactly what the Profit and Loss report shows for the same dates: income as credits less debits, expenses as debits less credits, over the branch's active income and expense accounts.
- **Variance** is positive when it is good, meaning income above budget or expenses below it. So a positive variance is always favourable.
- Budgets are entered as positive figures, so a contra account's budget is shown with the P&L's sign. Sales returns budgeted at 1,000 read −1,000, beside actual returns that read negative too.

## Project reports

Four reports read project accounting, under **Projects** in the list. Running them needs **projects.view** as well as access to reports.

- **Project Profitability**: one row per project. **Revenue** is what the project's income rows add up to, credits less debits, so a sales return or a discount given comes off it. **Cost** is its expense rows, debits less credits. **Profit** is the difference, and **Margin %** is profit as a share of revenue, left blank when there is no revenue. Rows that name no project are not counted. The report takes an optional **From** and **To** date.
- **Project Budget vs Actual**: each project's **cost budget** against the expense rows posted to it, and its tasks' **budget hours** against the hours logged, with the variance of each. Budget used % shows how much of the cost budget is spent. A budget is for the whole job, so this report has no period. A project with no cost budget shows no variance.
- **Project Unbilled Work**: what is waiting to be billed, valued the way **Add project items** on an invoice would bill it. **Time** is unbilled billable hours at the project's, the task's or the person's rate. **Expenses** are posted spend-money and bill lines marked **Charge on**, with their markup. **Milestones** are those not yet invoiced. A project with nothing waiting is left out.
- **Project Time by User**: hours logged in the period for each project task and person. The hours are split into billable, billed and non-billable.

A project's income and costs come from the ledger rows tagged with it. That covers manual journals, money documents, and sales and purchase lines. See [Projects](accounting#projects).

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



