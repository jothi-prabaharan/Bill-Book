using Master.Entity.TableEntities;

namespace Master.Repository.SeedData;

public static class MenuSeed
{
    public static IReadOnlyList<Menu> Build() =>
    [
        // Dashboard
        new Menu
        {
            MenuId = 1,
            Code = "dashboard",
            Name = "Dashboard",
            Icon = "home",
            DisplayOrder = 1,
            IsActive = true,
            SubMenus =
            [
                new SubMenu
                {
                    SubMenuId = 1,
                    MenuId = 1,
                    Code = "dashboard",
                    Name = "Dashboard",
                    RoutePath = "/dashboard",
                    Icon = "dashboard",
                    DisplayOrder = 1,
                    IsActive = true,
                    Permissions =
                    [
                        new SubMenuPermission { SubMenuPermissionId = 1, SubMenuId = 1, PermissionCode = "dashboard.view", Action = "view", Module = "dashboard" },
                    ]
                },
            ]
        },

        // Sales
        new Menu
        {
            MenuId = 2,
            Code = "sales",
            Name = "Sales",
            Icon = "shopping_cart",
            DisplayOrder = 2,
            IsActive = true,
            SubMenus =
            [
                new SubMenu
                {
                    SubMenuId = 2,
                    MenuId = 2,
                    Code = "sales_invoice",
                    Name = "Invoice",
                    RoutePath = "/sales/invoices",
                    Icon = "receipt",
                    DisplayOrder = 1,
                    IsActive = true,
                    Permissions =
                    [
                        new SubMenuPermission { SubMenuPermissionId = 2, SubMenuId = 2, PermissionCode = "sales.view", Action = "view", Module = "sales" },
                        new SubMenuPermission { SubMenuPermissionId = 3, SubMenuId = 2, PermissionCode = "sales.create", Action = "create", Module = "sales" },
                        new SubMenuPermission { SubMenuPermissionId = 4, SubMenuId = 2, PermissionCode = "sales.edit", Action = "edit", Module = "sales" },
                        new SubMenuPermission { SubMenuPermissionId = 5, SubMenuId = 2, PermissionCode = "sales.delete", Action = "delete", Module = "sales" },
                        new SubMenuPermission { SubMenuPermissionId = 6, SubMenuId = 2, PermissionCode = "sales.approve", Action = "approve", Module = "sales" },
                        new SubMenuPermission { SubMenuPermissionId = 7, SubMenuId = 2, PermissionCode = "sales.void", Action = "void", Module = "sales" },
                        new SubMenuPermission { SubMenuPermissionId = 8, SubMenuId = 2, PermissionCode = "sales.print", Action = "print", Module = "sales" },
                        new SubMenuPermission { SubMenuPermissionId = 9, SubMenuId = 2, PermissionCode = "sales.export", Action = "export", Module = "sales" },
                        new SubMenuPermission { SubMenuPermissionId = 10, SubMenuId = 2, PermissionCode = "sales.import", Action = "import", Module = "sales" },
                        new SubMenuPermission { SubMenuPermissionId = 11, SubMenuId = 2, PermissionCode = "sales.AllUserData", Action = "AllUserData", Module = "sales" },
                    ]
                },
                new SubMenu
                {
                    SubMenuId = 3,
                    MenuId = 2,
                    Code = "sales_order",
                    Name = "Sales Order",
                    RoutePath = "/sales/orders",
                    Icon = "assignment",
                    DisplayOrder = 2,
                    IsActive = true,
                    Permissions =
                    [
                        new SubMenuPermission { SubMenuPermissionId = 12, SubMenuId = 3, PermissionCode = "sales.view", Action = "view", Module = "sales" },
                        new SubMenuPermission { SubMenuPermissionId = 13, SubMenuId = 3, PermissionCode = "sales.create", Action = "create", Module = "sales" },
                        new SubMenuPermission { SubMenuPermissionId = 14, SubMenuId = 3, PermissionCode = "sales.edit", Action = "edit", Module = "sales" },
                        new SubMenuPermission { SubMenuPermissionId = 15, SubMenuId = 3, PermissionCode = "sales.approve", Action = "approve", Module = "sales" },
                        new SubMenuPermission { SubMenuPermissionId = 16, SubMenuId = 3, PermissionCode = "sales.void", Action = "void", Module = "sales" },
                        new SubMenuPermission { SubMenuPermissionId = 17, SubMenuId = 3, PermissionCode = "sales.print", Action = "print", Module = "sales" },
                        new SubMenuPermission { SubMenuPermissionId = 18, SubMenuId = 3, PermissionCode = "sales.export", Action = "export", Module = "sales" },
                    ]
                },
                new SubMenu
                {
                    SubMenuId = 4,
                    MenuId = 2,
                    Code = "sales_quote",
                    Name = "Quote",
                    RoutePath = "/sales/quotes",
                    Icon = "description",
                    DisplayOrder = 3,
                    IsActive = true,
                    Permissions =
                    [
                        new SubMenuPermission { SubMenuPermissionId = 19, SubMenuId = 4, PermissionCode = "sales.view", Action = "view", Module = "sales" },
                        new SubMenuPermission { SubMenuPermissionId = 20, SubMenuId = 4, PermissionCode = "sales.create", Action = "create", Module = "sales" },
                        new SubMenuPermission { SubMenuPermissionId = 21, SubMenuId = 4, PermissionCode = "sales.edit", Action = "edit", Module = "sales" },
                        new SubMenuPermission { SubMenuPermissionId = 22, SubMenuId = 4, PermissionCode = "sales.approve", Action = "approve", Module = "sales" },
                        new SubMenuPermission { SubMenuPermissionId = 23, SubMenuId = 4, PermissionCode = "sales.void", Action = "void", Module = "sales" },
                        new SubMenuPermission { SubMenuPermissionId = 24, SubMenuId = 4, PermissionCode = "sales.print", Action = "print", Module = "sales" },
                        new SubMenuPermission { SubMenuPermissionId = 25, SubMenuId = 4, PermissionCode = "sales.export", Action = "export", Module = "sales" },
                    ]
                },
                new SubMenu
                {
                    SubMenuId = 5,
                    MenuId = 2,
                    Code = "sales_delivery_challan",
                    Name = "Delivery Challan",
                    RoutePath = "/sales/delivery-challans",
                    Icon = "local_shipping",
                    DisplayOrder = 4,
                    IsActive = true,
                    Permissions =
                    [
                        new SubMenuPermission { SubMenuPermissionId = 26, SubMenuId = 5, PermissionCode = "sales.view", Action = "view", Module = "sales" },
                        new SubMenuPermission { SubMenuPermissionId = 27, SubMenuId = 5, PermissionCode = "sales.create", Action = "create", Module = "sales" },
                        new SubMenuPermission { SubMenuPermissionId = 28, SubMenuId = 5, PermissionCode = "sales.edit", Action = "edit", Module = "sales" },
                        new SubMenuPermission { SubMenuPermissionId = 29, SubMenuId = 5, PermissionCode = "sales.void", Action = "void", Module = "sales" },
                        new SubMenuPermission { SubMenuPermissionId = 30, SubMenuId = 5, PermissionCode = "sales.print", Action = "print", Module = "sales" },
                        new SubMenuPermission { SubMenuPermissionId = 31, SubMenuId = 5, PermissionCode = "sales.export", Action = "export", Module = "sales" },
                    ]
                },
                new SubMenu
                {
                    SubMenuId = 6,
                    MenuId = 2,
                    Code = "sales_credit_note",
                    Name = "Credit Note",
                    RoutePath = "/sales/credit-notes",
                    Icon = "credit_card",
                    DisplayOrder = 5,
                    IsActive = true,
                    Permissions =
                    [
                        new SubMenuPermission { SubMenuPermissionId = 32, SubMenuId = 6, PermissionCode = "sales.view", Action = "view", Module = "sales" },
                        new SubMenuPermission { SubMenuPermissionId = 33, SubMenuId = 6, PermissionCode = "sales.create", Action = "create", Module = "sales" },
                        new SubMenuPermission { SubMenuPermissionId = 34, SubMenuId = 6, PermissionCode = "sales.edit", Action = "edit", Module = "sales" },
                        new SubMenuPermission { SubMenuPermissionId = 35, SubMenuId = 6, PermissionCode = "sales.void", Action = "void", Module = "sales" },
                        new SubMenuPermission { SubMenuPermissionId = 36, SubMenuId = 6, PermissionCode = "sales.print", Action = "print", Module = "sales" },
                        new SubMenuPermission { SubMenuPermissionId = 37, SubMenuId = 6, PermissionCode = "sales.export", Action = "export", Module = "sales" },
                    ]
                },
                new SubMenu
                {
                    SubMenuId = 7,
                    MenuId = 2,
                    Code = "sales_pos",
                    Name = "POS Sale",
                    RoutePath = "/sales/pos",
                    Icon = "point_of_sale",
                    DisplayOrder = 6,
                    IsActive = true,
                    Permissions =
                    [
                        new SubMenuPermission { SubMenuPermissionId = 38, SubMenuId = 7, PermissionCode = "sales.view", Action = "view", Module = "sales" },
                        new SubMenuPermission { SubMenuPermissionId = 39, SubMenuId = 7, PermissionCode = "sales.create", Action = "create", Module = "sales" },
                        new SubMenuPermission { SubMenuPermissionId = 40, SubMenuId = 7, PermissionCode = "sales.print", Action = "print", Module = "sales" },
                    ]
                },
            ]
        },

        // Purchase
        new Menu
        {
            MenuId = 3,
            Code = "purchase",
            Name = "Purchase",
            Icon = "inventory",
            DisplayOrder = 3,
            IsActive = true,
            SubMenus =
            [
                new SubMenu
                {
                    SubMenuId = 8,
                    MenuId = 3,
                    Code = "purchase_bill",
                    Name = "Bill",
                    RoutePath = "/purchase/bills",
                    Icon = "receipt_long",
                    DisplayOrder = 1,
                    IsActive = true,
                    Permissions =
                    [
                        new SubMenuPermission { SubMenuPermissionId = 41, SubMenuId = 8, PermissionCode = "purchase.view", Action = "view", Module = "purchase" },
                        new SubMenuPermission { SubMenuPermissionId = 42, SubMenuId = 8, PermissionCode = "purchase.create", Action = "create", Module = "purchase" },
                        new SubMenuPermission { SubMenuPermissionId = 43, SubMenuId = 8, PermissionCode = "purchase.edit", Action = "edit", Module = "purchase" },
                        new SubMenuPermission { SubMenuPermissionId = 44, SubMenuId = 8, PermissionCode = "purchase.delete", Action = "delete", Module = "purchase" },
                        new SubMenuPermission { SubMenuPermissionId = 45, SubMenuId = 8, PermissionCode = "purchase.approve", Action = "approve", Module = "purchase" },
                        new SubMenuPermission { SubMenuPermissionId = 46, SubMenuId = 8, PermissionCode = "purchase.void", Action = "void", Module = "purchase" },
                        new SubMenuPermission { SubMenuPermissionId = 47, SubMenuId = 8, PermissionCode = "purchase.print", Action = "print", Module = "purchase" },
                        new SubMenuPermission { SubMenuPermissionId = 48, SubMenuId = 8, PermissionCode = "purchase.export", Action = "export", Module = "purchase" },
                        new SubMenuPermission { SubMenuPermissionId = 49, SubMenuId = 8, PermissionCode = "purchase.import", Action = "import", Module = "purchase" },
                        new SubMenuPermission { SubMenuPermissionId = 50, SubMenuId = 8, PermissionCode = "purchase.AllUserData", Action = "AllUserData", Module = "purchase" },
                    ]
                },
                new SubMenu
                {
                    SubMenuId = 9,
                    MenuId = 3,
                    Code = "purchase_order",
                    Name = "Purchase Order",
                    RoutePath = "/purchase/orders",
                    Icon = "shopping_cart",
                    DisplayOrder = 2,
                    IsActive = true,
                    Permissions =
                    [
                        new SubMenuPermission { SubMenuPermissionId = 51, SubMenuId = 9, PermissionCode = "purchase.view", Action = "view", Module = "purchase" },
                        new SubMenuPermission { SubMenuPermissionId = 52, SubMenuId = 9, PermissionCode = "purchase.create", Action = "create", Module = "purchase" },
                        new SubMenuPermission { SubMenuPermissionId = 53, SubMenuId = 9, PermissionCode = "purchase.edit", Action = "edit", Module = "purchase" },
                        new SubMenuPermission { SubMenuPermissionId = 54, SubMenuId = 9, PermissionCode = "purchase.approve", Action = "approve", Module = "purchase" },
                        new SubMenuPermission { SubMenuPermissionId = 55, SubMenuId = 9, PermissionCode = "purchase.void", Action = "void", Module = "purchase" },
                        new SubMenuPermission { SubMenuPermissionId = 56, SubMenuId = 9, PermissionCode = "purchase.print", Action = "print", Module = "purchase" },
                        new SubMenuPermission { SubMenuPermissionId = 57, SubMenuId = 9, PermissionCode = "purchase.export", Action = "export", Module = "purchase" },
                    ]
                },
                new SubMenu
                {
                    SubMenuId = 10,
                    MenuId = 3,
                    Code = "purchase_goods_receipt",
                    Name = "Goods Receipt",
                    RoutePath = "/purchase/goods-receipts",
                    Icon = "inventory_2",
                    DisplayOrder = 3,
                    IsActive = true,
                    Permissions =
                    [
                        new SubMenuPermission { SubMenuPermissionId = 58, SubMenuId = 10, PermissionCode = "purchase.view", Action = "view", Module = "purchase" },
                        new SubMenuPermission { SubMenuPermissionId = 59, SubMenuId = 10, PermissionCode = "purchase.create", Action = "create", Module = "purchase" },
                        new SubMenuPermission { SubMenuPermissionId = 60, SubMenuId = 10, PermissionCode = "purchase.edit", Action = "edit", Module = "purchase" },
                        new SubMenuPermission { SubMenuPermissionId = 61, SubMenuId = 10, PermissionCode = "purchase.void", Action = "void", Module = "purchase" },
                        new SubMenuPermission { SubMenuPermissionId = 62, SubMenuId = 10, PermissionCode = "purchase.print", Action = "print", Module = "purchase" },
                        new SubMenuPermission { SubMenuPermissionId = 63, SubMenuId = 10, PermissionCode = "purchase.export", Action = "export", Module = "purchase" },
                    ]
                },
                new SubMenu
                {
                    SubMenuId = 11,
                    MenuId = 3,
                    Code = "purchase_debit_note",
                    Name = "Debit Note",
                    RoutePath = "/purchase/debit-notes",
                    Icon = "note",
                    DisplayOrder = 4,
                    IsActive = true,
                    Permissions =
                    [
                        new SubMenuPermission { SubMenuPermissionId = 64, SubMenuId = 11, PermissionCode = "purchase.view", Action = "view", Module = "purchase" },
                        new SubMenuPermission { SubMenuPermissionId = 65, SubMenuId = 11, PermissionCode = "purchase.create", Action = "create", Module = "purchase" },
                        new SubMenuPermission { SubMenuPermissionId = 66, SubMenuId = 11, PermissionCode = "purchase.edit", Action = "edit", Module = "purchase" },
                        new SubMenuPermission { SubMenuPermissionId = 67, SubMenuId = 11, PermissionCode = "purchase.void", Action = "void", Module = "purchase" },
                        new SubMenuPermission { SubMenuPermissionId = 68, SubMenuId = 11, PermissionCode = "purchase.print", Action = "print", Module = "purchase" },
                        new SubMenuPermission { SubMenuPermissionId = 69, SubMenuId = 11, PermissionCode = "purchase.export", Action = "export", Module = "purchase" },
                    ]
                },
            ]
        },

        // Inventory
        new Menu
        {
            MenuId = 4,
            Code = "inventory",
            Name = "Inventory",
            Icon = "warehouse",
            DisplayOrder = 4,
            IsActive = true,
            SubMenus =
            [
                new SubMenu
                {
                    SubMenuId = 12,
                    MenuId = 4,
                    Code = "inventory_items",
                    Name = "Items",
                    RoutePath = "/inventory/items",
                    Icon = "inventory",
                    DisplayOrder = 1,
                    IsActive = true,
                    Permissions =
                    [
                        new SubMenuPermission { SubMenuPermissionId = 70, SubMenuId = 12, PermissionCode = "inventory.view", Action = "view", Module = "inventory" },
                        new SubMenuPermission { SubMenuPermissionId = 71, SubMenuId = 12, PermissionCode = "inventory.create", Action = "create", Module = "inventory" },
                        new SubMenuPermission { SubMenuPermissionId = 72, SubMenuId = 12, PermissionCode = "inventory.edit", Action = "edit", Module = "inventory" },
                        new SubMenuPermission { SubMenuPermissionId = 73, SubMenuId = 12, PermissionCode = "inventory.delete", Action = "delete", Module = "inventory" },
                        new SubMenuPermission { SubMenuPermissionId = 74, SubMenuId = 12, PermissionCode = "inventory.print", Action = "print", Module = "inventory" },
                        new SubMenuPermission { SubMenuPermissionId = 75, SubMenuId = 12, PermissionCode = "inventory.export", Action = "export", Module = "inventory" },
                        new SubMenuPermission { SubMenuPermissionId = 76, SubMenuId = 12, PermissionCode = "inventory.import", Action = "import", Module = "inventory" },
                        new SubMenuPermission { SubMenuPermissionId = 77, SubMenuId = 12, PermissionCode = "inventory.AllUserData", Action = "AllUserData", Module = "inventory" },
                    ]
                },
                new SubMenu
                {
                    SubMenuId = 13,
                    MenuId = 4,
                    Code = "inventory_categories",
                    Name = "Categories",
                    RoutePath = "/inventory/categories",
                    Icon = "category",
                    DisplayOrder = 2,
                    IsActive = true,
                    Permissions =
                    [
                        new SubMenuPermission { SubMenuPermissionId = 78, SubMenuId = 13, PermissionCode = "inventory.view", Action = "view", Module = "inventory" },
                        new SubMenuPermission { SubMenuPermissionId = 79, SubMenuId = 13, PermissionCode = "inventory.create", Action = "create", Module = "inventory" },
                        new SubMenuPermission { SubMenuPermissionId = 80, SubMenuId = 13, PermissionCode = "inventory.edit", Action = "edit", Module = "inventory" },
                        new SubMenuPermission { SubMenuPermissionId = 81, SubMenuId = 13, PermissionCode = "inventory.delete", Action = "delete", Module = "inventory" },
                    ]
                },
                new SubMenu
                {
                    SubMenuId = 14,
                    MenuId = 4,
                    Code = "inventory_stock",
                    Name = "Stock",
                    RoutePath = "/inventory/stock",
                    Icon = "storage",
                    DisplayOrder = 3,
                    IsActive = true,
                    Permissions =
                    [
                        new SubMenuPermission { SubMenuPermissionId = 82, SubMenuId = 14, PermissionCode = "inventory.view", Action = "view", Module = "inventory" },
                        new SubMenuPermission { SubMenuPermissionId = 83, SubMenuId = 14, PermissionCode = "inventory.export", Action = "export", Module = "inventory" },
                    ]
                },
                new SubMenu
                {
                    SubMenuId = 15,
                    MenuId = 4,
                    Code = "inventory_warehouses",
                    Name = "Warehouses",
                    RoutePath = "/inventory/warehouses",
                    Icon = "warehouse",
                    DisplayOrder = 4,
                    IsActive = true,
                    Permissions =
                    [
                        new SubMenuPermission { SubMenuPermissionId = 84, SubMenuId = 15, PermissionCode = "inventory.view", Action = "view", Module = "inventory" },
                        new SubMenuPermission { SubMenuPermissionId = 85, SubMenuId = 15, PermissionCode = "inventory.create", Action = "create", Module = "inventory" },
                        new SubMenuPermission { SubMenuPermissionId = 86, SubMenuId = 15, PermissionCode = "inventory.edit", Action = "edit", Module = "inventory" },
                    ]
                },
                new SubMenu
                {
                    SubMenuId = 16,
                    MenuId = 4,
                    Code = "inventory_uom",
                    Name = "Units of Measure",
                    RoutePath = "/inventory/uom",
                    Icon = "straighten",
                    DisplayOrder = 5,
                    IsActive = true,
                    Permissions =
                    [
                        new SubMenuPermission { SubMenuPermissionId = 87, SubMenuId = 16, PermissionCode = "inventory.view", Action = "view", Module = "inventory" },
                        new SubMenuPermission { SubMenuPermissionId = 88, SubMenuId = 16, PermissionCode = "inventory.create", Action = "create", Module = "inventory" },
                        new SubMenuPermission { SubMenuPermissionId = 89, SubMenuId = 16, PermissionCode = "inventory.edit", Action = "edit", Module = "inventory" },
                    ]
                },
                new SubMenu
                {
                    SubMenuId = 17,
                    MenuId = 4,
                    Code = "inventory_stock_adjustment",
                    Name = "Stock Adjustment",
                    RoutePath = "/inventory/stock-adjustments",
                    Icon = "tune",
                    DisplayOrder = 6,
                    IsActive = true,
                    Permissions =
                    [
                        new SubMenuPermission { SubMenuPermissionId = 90, SubMenuId = 17, PermissionCode = "inventory.view", Action = "view", Module = "inventory" },
                        new SubMenuPermission { SubMenuPermissionId = 91, SubMenuId = 17, PermissionCode = "inventory.create", Action = "create", Module = "inventory" },
                        new SubMenuPermission { SubMenuPermissionId = 92, SubMenuId = 17, PermissionCode = "inventory.void", Action = "void", Module = "inventory" },
                    ]
                },
            ]
        },

        // Accounting (label is "Accounts" per UI rule)
        new Menu
        {
            MenuId = 5,
            Code = "accounting",
            Name = "Accounts",
            Icon = "account_balance",
            DisplayOrder = 5,
            IsActive = true,
            SubMenus =
            [
                new SubMenu
                {
                    SubMenuId = 18,
                    MenuId = 5,
                    Code = "accounting_chart",
                    Name = "Chart of Accounts",
                    RoutePath = "/accounting/chart-of-accounts",
                    Icon = "account_tree",
                    DisplayOrder = 1,
                    IsActive = true,
                    Permissions =
                    [
                        new SubMenuPermission { SubMenuPermissionId = 93, SubMenuId = 18, PermissionCode = "accounting.view", Action = "view", Module = "accounting" },
                        new SubMenuPermission { SubMenuPermissionId = 94, SubMenuId = 18, PermissionCode = "accounting.create", Action = "create", Module = "accounting" },
                        new SubMenuPermission { SubMenuPermissionId = 95, SubMenuId = 18, PermissionCode = "accounting.edit", Action = "edit", Module = "accounting" },
                        new SubMenuPermission { SubMenuPermissionId = 96, SubMenuId = 18, PermissionCode = "accounting.delete", Action = "delete", Module = "accounting" },
                        new SubMenuPermission { SubMenuPermissionId = 97, SubMenuId = 18, PermissionCode = "accounting.print", Action = "print", Module = "accounting" },
                        new SubMenuPermission { SubMenuPermissionId = 98, SubMenuId = 18, PermissionCode = "accounting.export", Action = "export", Module = "accounting" },
                        new SubMenuPermission { SubMenuPermissionId = 99, SubMenuId = 18, PermissionCode = "accounting.AllUserData", Action = "AllUserData", Module = "accounting" },
                    ]
                },
                new SubMenu
                {
                    SubMenuId = 19,
                    MenuId = 5,
                    Code = "accounting_journal",
                    Name = "Journal Entries",
                    RoutePath = "/accounting/journals",
                    Icon = "edit_note",
                    DisplayOrder = 2,
                    IsActive = true,
                    Permissions =
                    [
                        new SubMenuPermission { SubMenuPermissionId = 100, SubMenuId = 19, PermissionCode = "accounting.view", Action = "view", Module = "accounting" },
                        new SubMenuPermission { SubMenuPermissionId = 101, SubMenuId = 19, PermissionCode = "accounting.create", Action = "create", Module = "accounting" },
                        new SubMenuPermission { SubMenuPermissionId = 102, SubMenuId = 19, PermissionCode = "accounting.edit", Action = "edit", Module = "accounting" },
                        new SubMenuPermission { SubMenuPermissionId = 103, SubMenuId = 19, PermissionCode = "accounting.approve", Action = "approve", Module = "accounting" },
                        new SubMenuPermission { SubMenuPermissionId = 104, SubMenuId = 19, PermissionCode = "accounting.void", Action = "void", Module = "accounting" },
                        new SubMenuPermission { SubMenuPermissionId = 105, SubMenuId = 19, PermissionCode = "accounting.print", Action = "print", Module = "accounting" },
                        new SubMenuPermission { SubMenuPermissionId = 106, SubMenuId = 19, PermissionCode = "accounting.export", Action = "export", Module = "accounting" },
                    ]
                },
                new SubMenu
                {
                    SubMenuId = 20,
                    MenuId = 5,
                    Code = "accounting_ledger",
                    Name = "Account Ledger",
                    RoutePath = "/accounting/ledger",
                    Icon = "ledger",
                    DisplayOrder = 3,
                    IsActive = true,
                    Permissions =
                    [
                        new SubMenuPermission { SubMenuPermissionId = 107, SubMenuId = 20, PermissionCode = "accounting.view", Action = "view", Module = "accounting" },
                        new SubMenuPermission { SubMenuPermissionId = 108, SubMenuId = 20, PermissionCode = "accounting.export", Action = "export", Module = "accounting" },
                    ]
                },
                new SubMenu
                {
                    SubMenuId = 21,
                    MenuId = 5,
                    Code = "accounting_trial_balance",
                    Name = "Trial Balance",
                    RoutePath = "/accounting/trial-balance",
                    Icon = "balance",
                    DisplayOrder = 4,
                    IsActive = true,
                    Permissions =
                    [
                        new SubMenuPermission { SubMenuPermissionId = 109, SubMenuId = 21, PermissionCode = "accounting.view", Action = "view", Module = "accounting" },
                        new SubMenuPermission { SubMenuPermissionId = 110, SubMenuId = 21, PermissionCode = "accounting.export", Action = "export", Module = "accounting" },
                    ]
                },
                new SubMenu
                {
                    SubMenuId = 22,
                    MenuId = 5,
                    Code = "accounting_opening_balance",
                    Name = "Opening Balance",
                    RoutePath = "/accounting/opening-balance",
                    Icon = "open_in_new",
                    DisplayOrder = 5,
                    IsActive = true,
                    Permissions =
                    [
                        new SubMenuPermission { SubMenuPermissionId = 111, SubMenuId = 22, PermissionCode = "accounting.view", Action = "view", Module = "accounting" },
                        new SubMenuPermission { SubMenuPermissionId = 112, SubMenuId = 22, PermissionCode = "accounting.create", Action = "create", Module = "accounting" },
                        new SubMenuPermission { SubMenuPermissionId = 113, SubMenuId = 22, PermissionCode = "accounting.edit", Action = "edit", Module = "accounting" },
                        new SubMenuPermission { SubMenuPermissionId = 114, SubMenuId = 22, PermissionCode = "accounting.void", Action = "void", Module = "accounting" },
                    ]
                },
                new SubMenu
                {
                    SubMenuId = 23,
                    MenuId = 5,
                    Code = "accounting_period_lock",
                    Name = "Period Locks",
                    RoutePath = "/accounting/period-locks",
                    Icon = "lock",
                    DisplayOrder = 6,
                    IsActive = true,
                    Permissions =
                    [
                        new SubMenuPermission { SubMenuPermissionId = 115, SubMenuId = 23, PermissionCode = "accounting.view", Action = "view", Module = "accounting" },
                        new SubMenuPermission { SubMenuPermissionId = 116, SubMenuId = 23, PermissionCode = "accounting.edit", Action = "edit", Module = "accounting" },
                    ]
                },
                new SubMenu
                {
                    SubMenuId = 24,
                    MenuId = 5,
                    Code = "accounting_tax",
                    Name = "Tax Master",
                    RoutePath = "/accounting/tax-master",
                    Icon = "receipt",
                    DisplayOrder = 7,
                    IsActive = true,
                    Permissions =
                    [
                        new SubMenuPermission { SubMenuPermissionId = 117, SubMenuId = 24, PermissionCode = "accounting.view", Action = "view", Module = "accounting" },
                        new SubMenuPermission { SubMenuPermissionId = 118, SubMenuId = 24, PermissionCode = "accounting.create", Action = "create", Module = "accounting" },
                        new SubMenuPermission { SubMenuPermissionId = 119, SubMenuId = 24, PermissionCode = "accounting.edit", Action = "edit", Module = "accounting" },
                        new SubMenuPermission { SubMenuPermissionId = 120, SubMenuId = 24, PermissionCode = "accounting.export", Action = "export", Module = "accounting" },
                    ]
                },
                new SubMenu
                {
                    SubMenuId = 25,
                    MenuId = 5,
                    Code = "accounting_payment_terms",
                    Name = "Payment Terms",
                    RoutePath = "/accounting/payment-terms",
                    Icon = "schedule",
                    DisplayOrder = 8,
                    IsActive = true,
                    Permissions =
                    [
                        new SubMenuPermission { SubMenuPermissionId = 121, SubMenuId = 25, PermissionCode = "accounting.view", Action = "view", Module = "accounting" },
                        new SubMenuPermission { SubMenuPermissionId = 122, SubMenuId = 25, PermissionCode = "accounting.create", Action = "create", Module = "accounting" },
                        new SubMenuPermission { SubMenuPermissionId = 123, SubMenuId = 25, PermissionCode = "accounting.edit", Action = "edit", Module = "accounting" },
                        new SubMenuPermission { SubMenuPermissionId = 124, SubMenuId = 25, PermissionCode = "accounting.delete", Action = "delete", Module = "accounting" },
                    ]
                },
                new SubMenu
                {
                    SubMenuId = 26,
                    MenuId = 5,
                    Code = "accounting_numbering",
                    Name = "Numbering Series",
                    RoutePath = "/accounting/numbering-series",
                    Icon = "format_list_numbered",
                    DisplayOrder = 9,
                    IsActive = true,
                    Permissions =
                    [
                        new SubMenuPermission { SubMenuPermissionId = 125, SubMenuId = 26, PermissionCode = "accounting.view", Action = "view", Module = "accounting" },
                        new SubMenuPermission { SubMenuPermissionId = 126, SubMenuId = 26, PermissionCode = "accounting.create", Action = "create", Module = "accounting" },
                        new SubMenuPermission { SubMenuPermissionId = 127, SubMenuId = 26, PermissionCode = "accounting.edit", Action = "edit", Module = "accounting" },
                        new SubMenuPermission { SubMenuPermissionId = 128, SubMenuId = 26, PermissionCode = "accounting.delete", Action = "delete", Module = "accounting" },
                    ]
                },
            ]
        },

        // Banking
        new Menu
        {
            MenuId = 6,
            Code = "banking",
            Name = "Banking",
            Icon = "account_balance_wallet",
            DisplayOrder = 6,
            IsActive = true,
            SubMenus =
            [
                new SubMenu
                {
                    SubMenuId = 27,
                    MenuId = 6,
                    Code = "banking_banks",
                    Name = "Banks",
                    RoutePath = "/banking/banks",
                    Icon = "business",
                    DisplayOrder = 1,
                    IsActive = true,
                    Permissions =
                    [
                        new SubMenuPermission { SubMenuPermissionId = 129, SubMenuId = 27, PermissionCode = "banking.view", Action = "view", Module = "banking" },
                        new SubMenuPermission { SubMenuPermissionId = 130, SubMenuId = 27, PermissionCode = "banking.create", Action = "create", Module = "banking" },
                        new SubMenuPermission { SubMenuPermissionId = 131, SubMenuId = 27, PermissionCode = "banking.edit", Action = "edit", Module = "banking" },
                        new SubMenuPermission { SubMenuPermissionId = 132, SubMenuId = 27, PermissionCode = "banking.delete", Action = "delete", Module = "banking" },
                    ]
                },
                new SubMenu
                {
                    SubMenuId = 28,
                    MenuId = 6,
                    Code = "banking_accounts",
                    Name = "Bank Accounts",
                    RoutePath = "/banking/accounts",
                    Icon = "account_balance",
                    DisplayOrder = 2,
                    IsActive = true,
                    Permissions =
                    [
                        new SubMenuPermission { SubMenuPermissionId = 133, SubMenuId = 28, PermissionCode = "banking.view", Action = "view", Module = "banking" },
                        new SubMenuPermission { SubMenuPermissionId = 134, SubMenuId = 28, PermissionCode = "banking.create", Action = "create", Module = "banking" },
                        new SubMenuPermission { SubMenuPermissionId = 135, SubMenuId = 28, PermissionCode = "banking.edit", Action = "edit", Module = "banking" },
                        new SubMenuPermission { SubMenuPermissionId = 136, SubMenuId = 28, PermissionCode = "banking.delete", Action = "delete", Module = "banking" },
                    ]
                },
                new SubMenu
                {
                    SubMenuId = 29,
                    MenuId = 6,
                    Code = "banking_spend_money",
                    Name = "Spend Money",
                    RoutePath = "/banking/spend-money",
                    Icon = "money_off",
                    DisplayOrder = 3,
                    IsActive = true,
                    Permissions =
                    [
                        new SubMenuPermission { SubMenuPermissionId = 137, SubMenuId = 29, PermissionCode = "banking.view", Action = "view", Module = "banking" },
                        new SubMenuPermission { SubMenuPermissionId = 138, SubMenuId = 29, PermissionCode = "banking.create", Action = "create", Module = "banking" },
                        new SubMenuPermission { SubMenuPermissionId = 139, SubMenuId = 29, PermissionCode = "banking.edit", Action = "edit", Module = "banking" },
                        new SubMenuPermission { SubMenuPermissionId = 140, SubMenuId = 29, PermissionCode = "banking.void", Action = "void", Module = "banking" },
                        new SubMenuPermission { SubMenuPermissionId = 141, SubMenuId = 29, PermissionCode = "banking.print", Action = "print", Module = "banking" },
                        new SubMenuPermission { SubMenuPermissionId = 142, SubMenuId = 29, PermissionCode = "banking.export", Action = "export", Module = "banking" },
                    ]
                },
                new SubMenu
                {
                    SubMenuId = 30,
                    MenuId = 6,
                    Code = "banking_receive_money",
                    Name = "Receive Money",
                    RoutePath = "/banking/receive-money",
                    Icon = "monetization_on",
                    DisplayOrder = 4,
                    IsActive = true,
                    Permissions =
                    [
                        new SubMenuPermission { SubMenuPermissionId = 143, SubMenuId = 30, PermissionCode = "banking.view", Action = "view", Module = "banking" },
                        new SubMenuPermission { SubMenuPermissionId = 144, SubMenuId = 30, PermissionCode = "banking.create", Action = "create", Module = "banking" },
                        new SubMenuPermission { SubMenuPermissionId = 145, SubMenuId = 30, PermissionCode = "banking.edit", Action = "edit", Module = "banking" },
                        new SubMenuPermission { SubMenuPermissionId = 146, SubMenuId = 30, PermissionCode = "banking.void", Action = "void", Module = "banking" },
                        new SubMenuPermission { SubMenuPermissionId = 147, SubMenuId = 30, PermissionCode = "banking.print", Action = "print", Module = "banking" },
                        new SubMenuPermission { SubMenuPermissionId = 148, SubMenuId = 30, PermissionCode = "banking.export", Action = "export", Module = "banking" },
                    ]
                },
                new SubMenu
                {
                    SubMenuId = 31,
                    MenuId = 6,
                    Code = "banking_transfer_money",
                    Name = "Transfer Money",
                    RoutePath = "/banking/transfer-money",
                    Icon = "swap_horiz",
                    DisplayOrder = 5,
                    IsActive = true,
                    Permissions =
                    [
                        new SubMenuPermission { SubMenuPermissionId = 149, SubMenuId = 31, PermissionCode = "banking.view", Action = "view", Module = "banking" },
                        new SubMenuPermission { SubMenuPermissionId = 150, SubMenuId = 31, PermissionCode = "banking.create", Action = "create", Module = "banking" },
                        new SubMenuPermission { SubMenuPermissionId = 151, SubMenuId = 31, PermissionCode = "banking.void", Action = "void", Module = "banking" },
                    ]
                },
                new SubMenu
                {
                    SubMenuId = 32,
                    MenuId = 6,
                    Code = "banking_statements",
                    Name = "Bank Statements",
                    RoutePath = "/banking/statements",
                    Icon = "description",
                    DisplayOrder = 6,
                    IsActive = true,
                    Permissions =
                    [
                        new SubMenuPermission { SubMenuPermissionId = 152, SubMenuId = 32, PermissionCode = "banking.view", Action = "view", Module = "banking" },
                        new SubMenuPermission { SubMenuPermissionId = 153, SubMenuId = 32, PermissionCode = "banking.create", Action = "create", Module = "banking" },
                        new SubMenuPermission { SubMenuPermissionId = 154, SubMenuId = 32, PermissionCode = "banking.edit", Action = "edit", Module = "banking" },
                        new SubMenuPermission { SubMenuPermissionId = 155, SubMenuId = 32, PermissionCode = "banking.print", Action = "print", Module = "banking" },
                        new SubMenuPermission { SubMenuPermissionId = 156, SubMenuId = 32, PermissionCode = "banking.export", Action = "export", Module = "banking" },
                    ]
                },
            ]
        },

        // Contacts
        new Menu
        {
            MenuId = 7,
            Code = "contacts",
            Name = "Contacts",
            Icon = "people",
            DisplayOrder = 7,
            IsActive = true,
            SubMenus =
            [
                new SubMenu
                {
                    SubMenuId = 33,
                    MenuId = 7,
                    Code = "contacts_list",
                    Name = "Contacts",
                    RoutePath = "/contacts",
                    Icon = "person",
                    DisplayOrder = 1,
                    IsActive = true,
                    Permissions =
                    [
                        new SubMenuPermission { SubMenuPermissionId = 157, SubMenuId = 33, PermissionCode = "contacts.view", Action = "view", Module = "contacts" },
                        new SubMenuPermission { SubMenuPermissionId = 158, SubMenuId = 33, PermissionCode = "contacts.create", Action = "create", Module = "contacts" },
                        new SubMenuPermission { SubMenuPermissionId = 159, SubMenuId = 33, PermissionCode = "contacts.edit", Action = "edit", Module = "contacts" },
                        new SubMenuPermission { SubMenuPermissionId = 160, SubMenuId = 33, PermissionCode = "contacts.delete", Action = "delete", Module = "contacts" },
                        new SubMenuPermission { SubMenuPermissionId = 161, SubMenuId = 33, PermissionCode = "contacts.export", Action = "export", Module = "contacts" },
                        new SubMenuPermission { SubMenuPermissionId = 162, SubMenuId = 33, PermissionCode = "contacts.AllUserData", Action = "AllUserData", Module = "contacts" },
                    ]
                },
            ]
        },

        // Reports
        new Menu
        {
            MenuId = 8,
            Code = "reports",
            Name = "Reports",
            Icon = "bar_chart",
            DisplayOrder = 8,
            IsActive = true,
            SubMenus =
            [
                new SubMenu
                {
                    SubMenuId = 34,
                    MenuId = 8,
                    Code = "reports_list",
                    Name = "Reports",
                    RoutePath = "/reports",
                    Icon = "list",
                    DisplayOrder = 1,
                    IsActive = true,
                    Permissions =
                    [
                        new SubMenuPermission { SubMenuPermissionId = 163, SubMenuId = 34, PermissionCode = "reports.view", Action = "view", Module = "reports" },
                        new SubMenuPermission { SubMenuPermissionId = 164, SubMenuId = 34, PermissionCode = "reports.export", Action = "export", Module = "reports" },
                        new SubMenuPermission { SubMenuPermissionId = 165, SubMenuId = 34, PermissionCode = "reports.AllUserData", Action = "AllUserData", Module = "reports" },
                    ]
                },
            ]
        },

        // Settings
        new Menu
        {
            MenuId = 9,
            Code = "settings",
            Name = "Settings",
            Icon = "settings",
            DisplayOrder = 9,
            IsActive = true,
            SubMenus =
            [
                new SubMenu
                {
                    SubMenuId = 35,
                    MenuId = 9,
                    Code = "settings_organization",
                    Name = "Organization",
                    RoutePath = "/settings/organization",
                    Icon = "business",
                    DisplayOrder = 1,
                    IsActive = true,
                    Permissions =
                    [
                        new SubMenuPermission { SubMenuPermissionId = 166, SubMenuId = 35, PermissionCode = "settings.view", Action = "view", Module = "settings" },
                        new SubMenuPermission { SubMenuPermissionId = 167, SubMenuId = 35, PermissionCode = "settings.edit", Action = "edit", Module = "settings" },
                    ]
                },
                new SubMenu
                {
                    SubMenuId = 36,
                    MenuId = 9,
                    Code = "settings_currencies",
                    Name = "Currencies",
                    RoutePath = "/settings/currencies",
                    Icon = "currency_exchange",
                    DisplayOrder = 2,
                    IsActive = true,
                    Permissions =
                    [
                        new SubMenuPermission { SubMenuPermissionId = 168, SubMenuId = 36, PermissionCode = "settings.view", Action = "view", Module = "settings" },
                        new SubMenuPermission { SubMenuPermissionId = 169, SubMenuId = 36, PermissionCode = "settings.edit", Action = "edit", Module = "settings" },
                    ]
                },
                new SubMenu
                {
                    SubMenuId = 37,
                    MenuId = 9,
                    Code = "settings_smtp",
                    Name = "Email Settings",
                    RoutePath = "/settings/email",
                    Icon = "email",
                    DisplayOrder = 3,
                    IsActive = true,
                    Permissions =
                    [
                        new SubMenuPermission { SubMenuPermissionId = 170, SubMenuId = 37, PermissionCode = "settings.view", Action = "view", Module = "settings" },
                        new SubMenuPermission { SubMenuPermissionId = 171, SubMenuId = 37, PermissionCode = "settings.edit", Action = "edit", Module = "settings" },
                    ]
                },
                new SubMenu
                {
                    SubMenuId = 38,
                    MenuId = 9,
                    Code = "settings_users",
                    Name = "Users",
                    RoutePath = "/settings/users",
                    Icon = "person_add",
                    DisplayOrder = 4,
                    IsActive = true,
                    Permissions =
                    [
                        new SubMenuPermission { SubMenuPermissionId = 172, SubMenuId = 38, PermissionCode = "settings.view", Action = "view", Module = "settings" },
                        new SubMenuPermission { SubMenuPermissionId = 173, SubMenuId = 38, PermissionCode = "settings.create", Action = "create", Module = "settings" },
                        new SubMenuPermission { SubMenuPermissionId = 174, SubMenuId = 38, PermissionCode = "settings.edit", Action = "edit", Module = "settings" },
                        new SubMenuPermission { SubMenuPermissionId = 175, SubMenuId = 38, PermissionCode = "settings.delete", Action = "delete", Module = "settings" },
                    ]
                },
                new SubMenu
                {
                    SubMenuId = 39,
                    MenuId = 9,
                    Code = "settings_roles",
                    Name = "Roles",
                    RoutePath = "/settings/roles",
                    Icon = "security",
                    DisplayOrder = 5,
                    IsActive = true,
                    Permissions =
                    [
                        new SubMenuPermission { SubMenuPermissionId = 176, SubMenuId = 39, PermissionCode = "settings.view", Action = "view", Module = "settings" },
                        new SubMenuPermission { SubMenuPermissionId = 177, SubMenuId = 39, PermissionCode = "settings.create", Action = "create", Module = "settings" },
                        new SubMenuPermission { SubMenuPermissionId = 178, SubMenuId = 39, PermissionCode = "settings.edit", Action = "edit", Module = "settings" },
                        new SubMenuPermission { SubMenuPermissionId = 179, SubMenuId = 39, PermissionCode = "settings.delete", Action = "delete", Module = "settings" },
                    ]
                },
                new SubMenu
                {
                    SubMenuId = 40,
                    MenuId = 9,
                    Code = "settings_branches",
                    Name = "Branches",
                    RoutePath = "/settings/branches",
                    Icon = "location_city",
                    DisplayOrder = 6,
                    IsActive = true,
                    Permissions =
                    [
                        new SubMenuPermission { SubMenuPermissionId = 180, SubMenuId = 40, PermissionCode = "settings.view", Action = "view", Module = "settings" },
                        new SubMenuPermission { SubMenuPermissionId = 181, SubMenuId = 40, PermissionCode = "settings.create", Action = "create", Module = "settings" },
                        new SubMenuPermission { SubMenuPermissionId = 182, SubMenuId = 40, PermissionCode = "settings.edit", Action = "edit", Module = "settings" },
                    ]
                },
                new SubMenu
                {
                    SubMenuId = 41,
                    MenuId = 9,
                    Code = "settings_config",
                    Name = "Configuration",
                    RoutePath = "/settings/configuration",
                    Icon = "tune",
                    DisplayOrder = 7,
                    IsActive = true,
                    Permissions =
                    [
                        new SubMenuPermission { SubMenuPermissionId = 183, SubMenuId = 41, PermissionCode = "settings.view", Action = "view", Module = "settings" },
                        new SubMenuPermission { SubMenuPermissionId = 184, SubMenuId = 41, PermissionCode = "settings.edit", Action = "edit", Module = "settings" },
                    ]
                },
            ]
        },
    ];
}