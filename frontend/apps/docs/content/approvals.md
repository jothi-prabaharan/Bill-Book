# Approvals

**Status: built.** Approval workflows are configured under **Settings › Approval workflows**, and the documents waiting on you are gathered under **Approvals › Waiting for me**.

## What can need approval

A workflow governs one kind of request. In RetailErp:

| Kind | What happens when it is approved |
|---|---|
| Purchase order, bill, debit note | Becomes ready to issue or post |
| Credit note | Becomes ready to post |
| Manual journal, spend money | **Post** is allowed |
| Stock adjustment | **Post** is allowed |
| Credit limit override | An invoice or sales order past the customer's credit limit may post or be confirmed |
| Sales discount override | An invoice or sales order with a line discounted past the limit may post or be confirmed |

HRMS and Payroll requests (leave, claims, salary revisions and the rest) are configured on the same page.

A request no active workflow covers is approved or posted as usual. If the approval rules cannot be read, the move is refused rather than let through.

## Setting up a workflow

**Settings › Approval workflows** lists every workflow. **New workflow** opens one, and you choose:

- **Applies to**, the kind of request it governs.
- **Effective from**, the date it starts. A later workflow for the same kind takes over from its own date.
- **Levels**, which run top to bottom. **Drag a level** by its handle, or use its arrows, to reorder.

For each level:

- **Label**, which is what the approver and the document screen show, for example "Accountant" or "Owner".
- **Approved by**. A RetailErp document is approved by **anyone holding a role** or by **a named user**. HRMS requests can also go to a reporting manager, a department head, a relationship or a named employee.
- **Only above amount**. When set, the level applies only to requests above that amount in base currency. What a request's amount is depends on its kind:
  - a credit-limit override uses the sale's total;
  - a discount override uses the discount given;
  - a manual journal uses its debit total;
  - a stock adjustment uses the value of the stock it brings in.
- **Comment required**, so the approver must say why even when approving.
- **Optional**, which marks a level that can be skipped.

Anyone with **settings.view** can read the page. Saving needs **settings.edit**.

## Approving

**Approvals › Waiting for me** lists every document waiting on you, from purchase, sales, accounts, banking and inventory, oldest first. A level waits on you when it names you, when you hold its role, or when you stand in as its approver's delegate.

For each document you can:

- **Approve** it. The next level then waits on its approver, or the document is approved.
- **Send back** or **Reject** it, both with a comment saying why.
- Open it by its number, to read it in full first.

The same actions are on each document's own **Approval** panel. Both go through the same rules:

- **Nobody approves two levels of the same chain.** Someone who approved one level cannot take the next, even if they hold its role.
- **Editing** a document in approval, or already approved, ends the chain. The approvals given stay on record, and the document must be submitted again.

A part of the product you cannot read is left out of your list. A part that could not be reached is named at the top, so nothing is missing without a word.

## What it does not do yet

- **Nobody is told** that something is waiting for them. The list shows it when they look.
- **Escalation** after a number of days is stored on a level but not acted on.
- **A delegate's list** does not yet show the documents waiting on the person they stand in for. They can act on those from the document itself.
