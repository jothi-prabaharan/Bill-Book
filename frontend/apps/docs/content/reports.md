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

## If a branch shows no reports

The report catalog is seeded per branch. A branch created before reporting existed has an empty catalog until it is seeded, which an administrator can trigger; it only adds what is missing, so it is safe to run again.



