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

**Export to Excel** produces the whole result — every row the filters allow, not just the page on screen — with the header frozen, amounts as numbers your spreadsheet can total, dates as real dates, and the subtotals as rows.

Exports are capped at 100,000 rows. Above that the export is refused with the row count rather than quietly truncated, because a file missing its last rows looks complete.

PDF export is not available yet.

## Sharing a report

The address bar carries your columns, filters, sorting and grouping. Copy the link and whoever opens it sees the same report — starting at the first page rather than yours. The back button and a refresh both keep your layout.

## On a phone

Below tablet width each row becomes a card, with the report's key columns as its title. Pivot is not offered at that width — a matrix has no card form.

## If a branch shows no reports

The report catalog is seeded per branch. A branch created before reporting existed has an empty catalog until it is seeded, which an administrator can trigger; it only adds what is missing, so it is safe to run again.
