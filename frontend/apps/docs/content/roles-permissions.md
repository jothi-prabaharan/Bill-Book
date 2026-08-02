# Roles & permissions

**Status: built** (API and screen).

## The model

Three tables: **Roles** → **RolePermissions** → **Permissions**, with **UserOrganizationRoles** assigning a role to a user *per organization*. One login can hold a different role in each organization it can reach.

Permissions are `{module}.{action}` — **12 modules × 10 actions = 120**.

Modules: dashboard, contacts, crm, inventory, sales, purchase, accounting, banking, reports, settings, support, platform
Actions: view, create, edit, approve, void, delete, print, export, import, AllUserData

`AllUserData` is not really an action — it is a **data scope**. Without it a user sees only records they created; with it they see the whole organization's. It is enforced as a query filter, not as a gate on an endpoint, so a permission check alone would not implement it.

`platform.*` is operator-only. It never appears in a customer's permission matrix and cannot be granted to a customer-defined role.

## The five system roles

| Role | Granted |
|---|---|
| **Owner** | Everything except `platform.*` |
| **Administrator** | Everything except `platform.*` |
| **Accountant** | All 10 actions in accounting, banking, reports, purchase — plus read-only contacts and inventory |
| **Sales** | All 10 actions in sales, contacts, crm — plus read-only inventory |
| **Viewer** | Every `.view` permission, nothing else |

Grants are **module-level**: a role that owns a module gets every action in it, including `approve`, `void` and `AllUserData`.

The two read-only additions are there because a role has to look at things it does not own in order to do its own job. Sales cannot sell what it cannot look up, and an accountant values stock and chases receivables that are held per contact. Neither grant allows a write: a salesperson still cannot edit an item, and an accountant still cannot edit a contact. Take them away on a copy of the role if that is not how you work.

## Which permission an endpoint asks for

The module is the one that **owns the data**, not the menu the screen sits under. GST rates and numbering series appear under Settings and belong to Accounting, so they ask for `accounting.*` — an accountant who could not edit a tax rate because of where it is filed would be a menu deciding an access rule.

| Screen | Asks for |
|---|---|
| Chart of accounts, sub-accounts, tax rates, payment terms, numbering series | `accounting.*` |
| Banks, bank accounts | `banking.*` |
| Contacts, contact roles, contact documents | `contacts.*` |
| Items, categories, stock, warehouses, units, purities | `inventory.*` |
| Users, roles, branches, currencies, configuration, email | `settings.*` |

Reading asks for `.view`, changing asks for `.edit`, and deleting asks for `.delete`. Country and state lists, currencies and the HSN/SAC master are reference data read by every module and ask only that you are signed in.

## What you see

The menu shows only what you can open. A role without `inventory.view` has no Inventory entry, and a bookmark or a typed address for one of its screens lands on Home rather than on a page that fails as it loads.

That is presentation, not protection. The permissions are read out of your sign-in token, which lives in your browser, so it is not something to rely on — every request is checked again on the server against a signed copy of the same claims, and that check is the one that decides. Hiding the menu entry is about not offering what you cannot have.

> Worth knowing when you assign these: Accountant and Sales can approve and void documents in their own modules, and can see every user's records there. If you need someone who can enter but not approve, create a customer role rather than using these.

## What a system role allows

| | System role | Customer role |
|---|---|---|
| Rename for display | ✅ | ✅ |
| Edit description | ✅ | ✅ |
| Change permissions | ❌ fixed | ✅ |
| Delete | ❌ never | ✅ soft delete |

Renaming changes the label only. The hidden `SystemName` is the identity that code and reports key on, so calling Accountant "Finance Lead" changes what users see and nothing about what it grants.

Deleting a customer role is a **soft delete** and is refused with `409` while any active user still holds it — a hard delete would orphan those assignments.

## The screen

`Settings → Roles`. The list shows every role with its active user count and permission count, and a System badge where applicable.

The editor renders the 120 permissions as a **module accordion** with select-all per module, since a flat grid of 120 checkboxes is unusable. On a phone it collapses to two columns per module. For a system role the whole matrix renders read-only.

```
GET    /api/roles                 list, system + own
GET    /api/roles/permissions     the matrix, grouped by module
GET    /api/roles/{id}            one role with its permission ids
POST   /api/roles                 create a customer role
PUT    /api/roles/{id}            update
DELETE /api/roles/{id}            soft delete, 409 when in use
```
