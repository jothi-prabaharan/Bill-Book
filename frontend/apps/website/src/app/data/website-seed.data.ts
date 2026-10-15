// -----------------------------------------------------------------------------
// Bill-Book Marketing & Corporate Website — Master Data & Seeding
// Universal Standalone Database Architecture, Dynamic Business Pricing & Add-ons
// Confined strictly to the Website module without touching core ERP projects
// -----------------------------------------------------------------------------

export interface BranchAddress {
  branchId: string;
  name: string;
  location: string;
  phone: string;
  email: string;
  badge: string;
}

export interface ApplicationOwnerProfile {
  legalName: string;
  tradeName: string;
  productBrand: string;
  supportPhone: string;
  salesPhone: string;
  primaryEmail: string;
  salesEmail: string;
  branches: BranchAddress[];
}

export interface ProjectScreenshot {
  id: string;
  title: string;
  category: string;
  imageSrc: string;
  description: string;
  liveProofPoints: string[];
}

// -----------------------------------------------------------------------------
// 1. Application Owner Profile (Strictly Chennai and Kumbakonam, 7010449938)
// -----------------------------------------------------------------------------
export const APPLICATION_OWNER_SEED: ApplicationOwnerProfile = {
  legalName: 'Eternal Pathway Technologies Pvt Ltd',
  tradeName: 'Bill-Book Cloud ERP',
  productBrand: 'Bill-Book RetailErp & Accounting Cloud',
  supportPhone: '7010449938',
  salesPhone: '7010449938',
  primaryEmail: 'info@yourbusiness.com',
  salesEmail: 'info@yourbusiness.com',
  branches: [
    {
      branchId: 'branch-chennai',
      name: 'Chennai',
      location: 'Chennai',
      phone: '7010449938',
      email: 'info@yourbusiness.com',
      badge: 'Chennai Branch',
    },
    {
      branchId: 'branch-kumbakonam',
      name: 'Kumbakonam',
      location: 'Kumbakonam',
      phone: '7010449938',
      email: 'info@yourbusiness.com',
      badge: 'Kumbakonam Branch',
    },
  ],
};

// -----------------------------------------------------------------------------
// 2. Universal Standalone Database Architecture Specification
// -----------------------------------------------------------------------------
export const STANDALONE_DB_FEATURE = {
  badge: 'Universal Architecture',
  headline: 'Dedicated Standalone Database for Every Organization',
  highlight:
    'A separate Standalone Database is exclusively allocated for your organization, ensuring maximum data privacy and ultra-fast performance.',
  description:
    'Unlike multi-tenant shared database architectures where noisy neighbors compete for IOPS, every Bill-Book customer is provisioned with their own physically isolated PostgreSQL database. Zero cross-tenant data leakage, independent encryption, and unthrottled computing performance.',
  specs: [
    'Dedicated PostgreSQL database instance per organization',
    'Physical schema boundary with zero cross-tenant query contamination',
    'Dedicated memory, caching & unthrottled disk IOPS',
    'Automated daily isolated backups & point-in-time recovery',
    'Independent database encryption keys and SOC2-ready isolation',
  ],
};

// -----------------------------------------------------------------------------
// 3. Dynamic Business-Type Pricing Tiers (Retail & Groceries base = 1500)
// -----------------------------------------------------------------------------
export interface BusinessPricingTier {
  id: string;
  name: string;
  badge: string;
  tagline: string;
  baseMonthlyPrice: number;
  features: string[];
}

export const BUSINESS_PRICING_TIERS_SEED: BusinessPricingTier[] = [
  {
    id: 'groceries',
    name: 'Retail & Groceries',
    badge: 'Retail & POS',
    tagline: 'High-speed barcode checkout, electronic weighing scale sync, and daily inventory.',
    baseMonthlyPrice: 1500, // 1500 per month for retail and groceries
    features: [
      'Sub-second barcode scanning and keyboard-only POS checkout',
      'Electronic weighing scale integration with tare weight calculation',
      'Dual-pricing (MRP vs Selling Price) with margin protection',
      'Daily cash counter drawer opening and closing reconciliation',
      'Fast thermal receipt printing (2-inch & 3-inch slips)',
      'Automated GST B2C and B2B sales invoices',
    ],
  },
  {
    id: 'pharma',
    name: 'Pharmacies & Healthcare',
    badge: 'Batch & Expiry',
    tagline: 'Batch number & expiry date tracking, Schedule drug registers, and doctor tagging.',
    baseMonthlyPrice: 1500, // 1500 per month
    features: [
      'First-Expiry, First-Out (FEFO) automated dispatch alerts',
      'Batch number and expiry date enforcement on purchase and sales',
      'Schedule H and H1 regulatory drug inspection registers',
      'Physician and doctor prescription tagging on invoices',
      'Generic chemical salt formulation substitute search',
      'Supplier medicine return notes with batch reconciliation',
    ],
  },
  {
    id: 'jewellery',
    name: 'Jewellery & Bullion',
    badge: 'Purity & Bullion',
    tagline: 'Daily 22K/24K purity market rate sync, hallmark HUID, and gold savings scheme.',
    baseMonthlyPrice: 4000, // 4000 per month
    features: [
      'Daily 22K (0.9160 factor), 24K and 18K gold/silver market rate sync per gram',
      'Making charges, stone weight deductions, and net metal calculations',
      'Hallmark HUID number registration for complete traceability',
      'Customer Gold Savings Deposit Scheme passbook and monthly collections',
      'Old gold exchange value estimation with purity testing deductions',
      'Comprehensive bullion sales register with GST metal classification',
    ],
  },
  {
    id: 'schools',
    name: 'Schools & Educational Institutions',
    badge: 'Institutional & SIS',
    tagline: 'Academic year student rosters, term tuition fee schedules, and concessions.',
    baseMonthlyPrice: 5000, // 5000 per month
    features: [
      'Academic year student roster with roll numbers and guardian profiles',
      'Multi-term tuition, laboratory, library, and transportation fee installments',
      'Sibling concessions, merit scholarships, and customized discount waivers',
      'Automated fee receipt voucher generation with student ledger balance',
      'Parent fee payment defaulter rosters with automated SMS/email alerts',
      'Institutional accounting with departmental cost center tracking',
    ],
  },
  {
    id: 'restaurant',
    name: 'Restaurants & Hospitality',
    badge: 'Dining & KOT',
    tagline: 'Table layout management, wireless Kitchen Order Tickets (KOT), and recipe BOM.',
    baseMonthlyPrice: 2200,
    features: [
      'Visual dining table floor view with live occupied/vacant states',
      'Instant wireless Kitchen Order Ticket (KOT) thermal routing',
      'Item modifiers (Extra Cheese, Less Spicy, Special Requests)',
      'Menu recipe bill-of-materials with automated ingredient deduction',
      'Aggregator and delivery order tracking with parcel packing slips',
    ],
  },
  {
    id: 'manufacturing',
    name: 'Discrete & Process Manufacturing',
    badge: 'BOM & Work Orders',
    tagline: 'Multi-level Bill of Materials (BOM), work orders, job work, and machine routing.',
    baseMonthlyPrice: 2000,
    features: [
      'Multi-level BOM with scrap percentages and machine routing steps',
      'Production work orders with raw material staging and consumption',
      'Job work order issue & receipt with material wastage reconciliation',
      'Batch quality control inspection logs and real-time audit trails',
      'Cost layer consumptions (FIFO / Weighted Average)',
    ],
  },
  {
    id: 'services',
    name: 'Services & Consulting',
    badge: 'Projects & Retainers',
    tagline: 'Milestone invoicing, timesheets, project cost tracking, and GST SAC code billing.',
    baseMonthlyPrice: 1500,
    features: [
      'Milestone-based billing and recurring retainer agreements',
      'Timesheet logging and consultant billable hour tracking',
      'Service Accounting Code (SAC) automated GST invoicing',
      'Project-wise profit & loss statement with expense allocation',
      'Client advance receipt vouchers with TDS deduction management',
      'Digital proforma invoice generation and payment tracking',
    ],
  },
];

// -----------------------------------------------------------------------------
// 4. Exactly 3-Box Subscription Structure (Per Month, Per Quarter, Per Year)
// -----------------------------------------------------------------------------
export interface PlanCycleBox {
  id: 'monthly' | 'quarterly' | 'yearly';
  boxNumber: string;
  name: string;
  subLabel: string;
  discountPercent: number;
  badge: string;
  badgeClass: string;
  highlight: boolean;
  periodMonths: number;
  ctaText: string;
  perks: string[];
}

export const PLAN_CYCLE_BOXES: PlanCycleBox[] = [
  {
    id: 'quarterly',
    boxNumber: 'Box 1',
    name: 'Per Quarter',
    subLabel: '3-month cycle tailored for seasonal operations',
    discountPercent: 10,
    badge: 'Save 10%',
    badgeClass: 'badge-save',
    highlight: false,
    periodMonths: 3,
    ctaText: 'Select Quarterly Plan',
    perks: [
      'Separate Standalone Database exclusively allocated',
      'Full ERP and double-entry accounting suite',
      '10% recurring cost advantage',
      'Priority technical support line (Chennai & Kumbakonam)',
      'Quarterly tax filing reconciliation reviews',
    ],
  },
  {
    id: 'yearly',
    boxNumber: 'Box 2',
    name: 'Per Year',
    subLabel: 'Maximum savings with guaranteed annual SLA',
    discountPercent: 20,
    badge: 'Save 20% • Best Value',
    badgeClass: 'badge-best',
    highlight: true,
    periodMonths: 12,
    ctaText: 'Select Yearly Plan →',
    perks: [
      'Separate Standalone Database exclusively allocated',
      'Everything in Quarterly Plan included',
      '20% massive annual savings (equivalent to 2.4 months free)',
      'Dedicated Solution Engineer for your business',
      'Free initial master data migration from legacy software',
      'Priority feature onboarding and custom print styling',
    ],
  },
];

// -----------------------------------------------------------------------------
// 5. Dedicated Add-Ons Section Catalog
// -----------------------------------------------------------------------------
export interface AddOnModule {
  id: string;
  title: string;
  priceLabel: string;
  monthlyPrice: number;
  unit: string;
  badge: string;
  description: string;
  features: string[];
}

export const ADDON_MODULES_SEED: AddOnModule[] = [
  {
    id: 'multi-branch',
    title: 'Multi-Branch Sync',
    priceLabel: '₹500 / branch / mo',
    monthlyPrice: 500,
    unit: 'per branch / mo',
    badge: 'Multi-Branch',
    description:
      'Connect unlimited branch locations with centralized consolidation, inter-branch stock transfers, and branch-scoped ledgers.',
    features: [
      'Head-office consolidated balance sheet',
      'Inter-branch stock transfer vouchers',
      'Branch-scoped user access control',
    ],
  },
  {
    id: 'e-invoicing',
    title: 'E-Invoicing & E-Way Bill',
    priceLabel: '₹400 / mo',
    monthlyPrice: 400,
    unit: 'per month',
    badge: 'GST Direct API',
    description:
      'Direct NIC portal integration with 1-click IRN generation, signed QR code thermal printing, and automated e-way bill dispatch.',
    features: [
      '1-click IRN generation without manual JSON',
      'Signed B2B QR code on tax invoices',
      'Automated GSTR-1 auto-population',
    ],
  },
  {
    id: 'whatsapp-sms',
    title: 'WhatsApp & SMS Gateway',
    priceLabel: '₹350 / mo',
    monthlyPrice: 350,
    unit: 'per month',
    badge: 'Customer Engagement',
    description:
      'Instant PDF bill delivery to customer WhatsApp, automated payment due reminders, and promotional festival SMS campaigns.',
    features: [
      'Official WhatsApp Cloud API delivery',
      'Automated overdue ledger payment reminders',
      'Custom message branding with company name',
    ],
  },
  {
    id: 'payroll-hrm',
    title: 'Biometric HR & Payroll',
    priceLabel: '₹600 / mo',
    monthlyPrice: 600,
    unit: 'per month',
    badge: 'Staff & Payroll',
    description:
      'Biometric punch machine sync, shift scheduling, automated salary calculation with PF, ESI, Professional Tax, and salary slips.',
    features: [
      'Biometric device API integration',
      'Automated monthly payslip generation',
      'Statutory PF and ESI compliance reports',
    ],
  },
  {
    id: 'restaurant-pos',
    title: 'Kitchen Order Ticket (KOT)',
    priceLabel: '₹450 / mo',
    monthlyPrice: 450,
    unit: 'per month',
    badge: 'Dining & Kitchen',
    description:
      'Wireless thermal kitchen printer routing, live table floor visual grid, item recipe modifiers, and captain ordering tablets.',
    features: [
      'Wireless multi-station thermal kitchen routing',
      'Live dining table occupancy layout',
      'Recipe ingredient bill of materials',
    ],
  },
  {
    id: 'label-designer',
    title: 'Custom Label Designer',
    priceLabel: '₹300 / mo',
    monthlyPrice: 300,
    unit: 'per month',
    badge: 'Printing Utility',
    description:
      'Visual drag-and-drop thermal label designer for barcode stickers, jewellery hallmark tags, shelf edge tags, and apparel price labels.',
    features: [
      'Drag-and-drop designer for thermal printers',
      'Custom barcode formats (Code 128, EAN-13, QR)',
      'Support for jewellery barbell tags & garment tags',
    ],
  },
];

// -----------------------------------------------------------------------------
// 6. Verified UI Screenshots Captured from Real Project
// -----------------------------------------------------------------------------
export const PROJECT_SCREENSHOTS_SEED: ProjectScreenshot[] = [
  {
    id: 'dashboard',
    title: 'Executive Financial Dashboard',
    category: 'Finance & Analytics',
    imageSrc: 'assets/screenshots/dashboard-overview.png',
    description:
      'Actual working Executive Dashboard featuring live Sales Today (₹1,84,250), Overdue Receivables, Net Profit (₹8,23,200), and Real-time Bank Accounts with Trend Charts.',
    liveProofPoints: [
      'Live P&L Statement (Revenue ₹58.48L, Net Profit ₹8.23L)',
      'Real-time cash & bank balances with visual sparkline trend lines',
      'Aging receivables breakdown (Largest and oldest outstanding balances)',
      'Top products by margin and units sold',
    ],
  },
  {
    id: 'purchases',
    title: 'Purchase Orders & Supply Chain',
    category: 'Supply Chain',
    imageSrc: 'assets/screenshots/purchase-orders.png',
    description:
      'Actual working purchase order management with multi-stage approval, vendor dispatch tracking, and financial year filters.',
    liveProofPoints: [
      'Real-time purchase order register (₹5,66,000 total)',
      'Open, PartlyReceived, Closed, and Cancelled status tabs',
      'Live vendor allocations (Coimbatore Yarns, Raja Packaging, Velan Dyeing)',
      'Integrated export and import actions',
    ],
  },
  {
    id: 'bills',
    title: 'Vendor Bills & Tax Reconciliation',
    category: 'Accounts Payable',
    imageSrc: 'assets/screenshots/purchase-bills.png',
    description:
      'Supplier bills registry with 3-way matching against purchase orders and automated input tax credit (ITC) calculations.',
    liveProofPoints: [
      'Real-time bills register with draft and posted lifecycle',
      'Total taxable & invoice amounts with standard currency formatting',
      'Direct navigation from bills to payment allocations',
      'Audit logging on every vendor financial transaction',
    ],
  },
  {
    id: 'inventory',
    title: 'Item Master & Multi-Unit Catalog',
    category: 'Inventory Management',
    imageSrc: 'assets/screenshots/inventory-items.png',
    description:
      'High-speed item catalog with instant barcode search, HSN/SAC classification, and reorder level alerts.',
    liveProofPoints: [
      'Instant barcode and item code lookups',
      'Multi-unit conversion factors (Weight, Volume, Pieces, Packs)',
      'Real-time stock valuation and on-hand balance checks',
      'Stock adjustments and automated transfer notes',
    ],
  },
  {
    id: 'banking',
    title: 'Banking Dashboard & Cash Counters',
    category: 'Cash & Banking',
    imageSrc: 'assets/screenshots/banking-dashboard.png',
    description:
      'Main cash counter dashboard showing ₹3,42,180 with statement trend reconciliation and spend/receive money shortcuts.',
    liveProofPoints: [
      'Interactive balance chart tracking statement vs book balance',
      'One-click Spend Money and Receive Money entries',
      'Direct bank statement CSV/Excel import',
      'Unreconciled transaction alerts and match suggestions',
    ],
  },
  {
    id: 'contacts',
    title: 'Master Contacts & GST Profiles',
    category: 'Master Data',
    imageSrc: 'assets/screenshots/contacts-master.png',
    description:
      'Unified contact master handling customers, vendors, and job workers with GSTIN and PAN validation.',
    liveProofPoints: [
      'Multi-role party assignment (Customer, Vendor, Job worker)',
      'Real-time GSTIN validation and State code lookups',
      'Credit limit tracking and outstanding balance enforcement',
      'Multiple contact persons, bank accounts, and addresses',
    ],
  },
  {
    id: 'purities',
    title: 'Jewellery Metal Purity Rates',
    category: 'Specialized Verticals',
    imageSrc: 'assets/screenshots/metal-purities.png',
    description:
      'Specialized bullion master calculating gold purity factors (e.g. 22K 0.9160) for dynamic piece pricing and HUID hallmark tags.',
    liveProofPoints: [
      'Configurable purity factor multiplier per metal karat',
      'Live gold & silver daily market rate updates',
      'Old gold exchange deductions and melt loss margins',
      'Customer gold savings deposit scheme passbook integrations',
    ],
  },
  {
    id: 'users',
    title: 'User Management & Role Directory',
    category: 'Security & Administration',
    imageSrc: 'assets/screenshots/users-management.png',
    description:
      'Actual user management directory from live project with multi-branch provisioning, role-based access control (RBAC), user invites, and active status tracking.',
    liveProofPoints: [
      'Multi-branch user provisioning and role-based access control (RBAC)',
      'Live active owner and user directory (Jothi Prabaharan, Owner)',
      'Instant email invitations with role and branch permissions assignment',
      'Dedicated standalone database per tenant with complete isolation',
    ],
  },
];
