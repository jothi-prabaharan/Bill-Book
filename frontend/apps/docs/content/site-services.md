# Site Services

Site services provides project-based job management, trade labour tracking, site inventory dispatches, and job billing for Service businesses.

## Key Capabilities

- **Sites & Projects**: Define customer project sites with geofence coordinates and dedicated site warehouses.
- **Trades & Laborers**: Manage daily/monthly labour rates, trades, assignments, and check-in / clock-out attendance.
- **Margin Rules**: Apply tiered margin resolution across Assignment → Site → Trade/Item → Branch scopes.
- **Stock Dispatches & Returns**: Dispatch materials to job sites and return unused stock back to central stores.
- **Material Usage & Approvals**: Log material consumption against site projects with supervisor approval.
- **Site Billing**: Bill labor and materials with separate SAC and HSN lines directly into Sales invoices.

## Masters & Tiered Margin Hierarchy

The margin engine resolves markups and margin additions using a strict hierarchical fallback:
1. **Assignment Scope**: Rate and margin overrides specific to an individual laborer's assignment on a project site.
2. **Site Scope**: Project-level margins applying to all labor or materials used on that site.
3. **Trade or Item Scope**: Trade-specific rate defaults (e.g. Electrical) or Item-specific material markup rules.
4. **Branch Scope**: The default baseline margin rule across the organization.

Cost rates and margin calculations are strictly privacy-guarded: only accounts carrying the `site.cost.view` permission can see cost values, margin percentages, and laborer wage baselines.

## Site Warehouses

Every site project automatically creates a dedicated site warehouse in the Inventory service (`WarehouseType.Site = 4`) linked via `inv.Warehouses.SiteId`. This provides isolated on-site stock accounting and supports physical dispatches and returns.

## Add-on Configuration

Site Services is available exclusively for branches with the `Service` business type through the `SiteServices` add-on. Enabling the add-on grants the branch access to inventory stock management and site operations.
