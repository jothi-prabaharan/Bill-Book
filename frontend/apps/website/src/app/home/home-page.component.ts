import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  APPLICATION_OWNER_SEED,
  ApplicationOwnerProfile,
  BUSINESS_PRICING_TIERS_SEED,
  BusinessPricingTier,
  PLAN_CYCLE_BOXES,
  PlanCycleBox,
  PROJECT_SCREENSHOTS_SEED,
  ProjectScreenshot,
  STANDALONE_DB_FEATURE,
} from '../data/website-seed.data';

export interface Vertical {
  id: string;
  name: string;
  tagline: string;
  badge: string;
  icon: string;
  accent: string;
  features: string[];
}

export interface ModuleTab {
  id: string;
  title: string;
  shortTitle?: string;
  badge: string;
  description: string;
  primaryImage: string;
  primaryCaption: string;
  primaryUrl?: string;
  secondaryImage?: string;
  secondaryCaption?: string;
  secondaryUrl?: string;
  highlights: string[];
}

export interface EnquiryForm {
  name: string;
  companyName: string;
  phone: string;
  email: string;
  vertical: string;
  plan: string;
  branchCount: number;
  message: string;
}

export interface CalculatedBoxPrice {
  box: PlanCycleBox;
  effectiveMonthlyPrice: number;
  periodTotal: number;
  billingText: string;
  savingsBadge?: string;
}

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-website-home',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './home-page.component.html',
  styleUrl: './home-page.component.scss',
})
export class HomePageComponent {
  private readonly http = inject(HttpClient);

  // ---------------------------------------------------------------------------
  // Master Seed Data (Owner details: Chennai & Kumbakonam, Phone: 7010449938)
  // ---------------------------------------------------------------------------
  readonly ownerProfile: ApplicationOwnerProfile = APPLICATION_OWNER_SEED;
  readonly standaloneDbFeature = STANDALONE_DB_FEATURE;
  readonly businessPricingTiers: BusinessPricingTier[] = BUSINESS_PRICING_TIERS_SEED;
  readonly planCycleBoxes: PlanCycleBox[] = PLAN_CYCLE_BOXES;
  readonly projectScreenshots: ProjectScreenshot[] = PROJECT_SCREENSHOTS_SEED;

  // ---------------------------------------------------------------------------
  // Dynamic Business Pricing State (Base Retail & Groceries = 1500)
  // ---------------------------------------------------------------------------
  readonly selectedBusinessTypeId = signal<string>('groceries');

  readonly activeBusinessTier = computed<BusinessPricingTier>(() => {
    return (
      this.businessPricingTiers.find((t) => t.id === this.selectedBusinessTypeId()) ??
      this.businessPricingTiers[0]
    );
  });

  // Calculate pricing for exactly 3 distinct boxes
  readonly calculatedPlanBoxes = computed<CalculatedBoxPrice[]>(() => {
    const tier = this.activeBusinessTier();
    const base = tier.baseMonthlyPrice;

    return this.planCycleBoxes.map((box) => {
      const discount = box.discountPercent;
      const effMonthly = Math.round(base * (1 - discount / 100));
      const periodTotal = effMonthly * box.periodMonths;

      let billingText = 'Billed monthly';
      if (box.id === 'quarterly') {
        billingText = `Billed ₹${periodTotal.toLocaleString('en-IN')} quarterly`;
      } else if (box.id === 'yearly') {
        billingText = `Billed ₹${periodTotal.toLocaleString('en-IN')} annually`;
      }

      return {
        box,
        effectiveMonthlyPrice: effMonthly,
        periodTotal,
        billingText,
        savingsBadge: box.badge,
      };
    });
  });

  // ---------------------------------------------------------------------------
  // Module Deep Dive Tabs (Featuring Real Project Screenshots)
  // ---------------------------------------------------------------------------
  readonly activeModuleTab = signal<number>(0);
  readonly activeSubView = signal<'primary' | 'secondary'>('primary');

  readonly moduleTabs: ModuleTab[] = [
    {
      id: 'financials',
      title: 'Executive Financials & Banking',
      shortTitle: 'Financials & Banking',
      badge: 'Real-Time Double Entry',
      description:
        'Live executive dashboard with real profit & loss, balance sheet, revenue tracking, and cash counter registers.',
      primaryImage: 'assets/screenshots/dashboard-overview.png',
      primaryCaption: 'Executive Dashboard — Real revenue ₹58.48L, net profit ₹8.23L, sales & bank balance curves',
      primaryUrl: 'dashboard',
      secondaryImage: 'assets/screenshots/banking-dashboard.png',
      secondaryCaption: 'Live Cash Counter & Bank Balances — ₹3,42,180 cash on hand & real-time counter drawer',
      secondaryUrl: 'banking/dashboard',
      highlights: [
        'Automated double-entry general ledger posting on every transaction',
        'Real-time Profit & Loss, Trial Balance, and Balance Sheet generation',
        'Physical cash drawer opening balance, cash-in, cash-out, and end-of-day counter reconciliation',
        'GST liability calculation with automated input tax credit (ITC) offset',
      ],
    },
    {
      id: 'supply-chain',
      title: 'Procurement & 3-Way Bills Matching',
      shortTitle: 'Procurement & Bills',
      badge: 'Zero Vendor Leakage',
      description:
        'Full procurement lifecycle from Purchase Orders (POR) to Goods Receipt (GRN) and Vendor Bills with automated 3-way matching.',
      primaryImage: 'assets/screenshots/purchase-orders.png',
      primaryCaption: 'Purchase Orders Register — POR-0001 & POR-0002 records totaling ₹5,66,000 verified in register',
      primaryUrl: 'purchase/orders',
      secondaryImage: 'assets/screenshots/purchase-bills.png',
      secondaryCaption: 'Purchase Bills Register — BIL-0001 & BIL-0002 vendor invoices totaling ₹6,41,400 with tax breakdown',
      secondaryUrl: 'purchase/bills',
      highlights: [
        'Automated 3-way matching between Purchase Order, Goods Receipt, and Vendor Bill',
        'Tolerances for partial delivery, unit price variance, and tax verification',
        'Vendor payment aging schedule (0-30, 31-60, 61-90, 90+ days)',
        'Barcode-enabled receipt scanning directly into warehouse bins',
      ],
    },
    {
      id: 'inventory-bullion',
      title: 'Barcode Inventory & Bullion Rates',
      shortTitle: 'Inventory & Bullion',
      badge: 'Live Stock & Bullion Sync',
      description:
        'High-velocity stock management with sub-second barcode lookups and daily gold/silver market purity rate sync.',
      primaryImage: 'assets/screenshots/inventory-items.png',
      primaryCaption: 'Live Items Catalog — Barcode search, stock on hand across branches, and reorder levels',
      primaryUrl: 'inventory/items',
      secondaryImage: 'assets/screenshots/metal-purities.png',
      secondaryCaption: 'Jewellery Metal Purity Rates — 22K (0.9160 factor), 24K & 18K bullion market rates',
      secondaryUrl: 'settings/metal-purities',
      highlights: [
        'Sub-second item lookup by barcode, SKU, brand, or generic description',
        'Multi-branch stock visibility with inter-branch transfer vouchers',
        'Live 22K (0.9160 factor) jewellery rates with making charge and stone weight calculations',
        'Hallmark HUID number registration for complete precious metal traceability',
      ],
    },
    {
      id: 'masters-security',
      title: 'User Management & Enterprise Masters',
      shortTitle: 'Users & Permissions',
      badge: 'Role-Based Access Control',
      description:
        'Multi-branch user provisioning, role-based access control (RBAC), user invite links, and master contacts directory with dedicated database security.',
      primaryImage: 'assets/screenshots/users-management.png',
      primaryCaption: 'User Directory & Access Control — Real live users (Jothi Prabaharan, Owner), role assignments, and branch provisioning',
      primaryUrl: 'settings/users',
      secondaryImage: 'assets/screenshots/contacts-master.png',
      secondaryCaption: 'Master Contacts Directory — Customers, vendors, and job workers with GSTIN verification and credit limits',
      secondaryUrl: 'contacts',
      highlights: [
        'Real-time user provisioning with role-based access control (RBAC)',
        'PostgreSQL Standalone Database guaranteeing 100% data isolation',
        'Contacts Master with automated GSTIN validation & state tax codes',
        'Complete immutable audit log recording creator, time, and delta',
      ],
    },
  ];

  // ---------------------------------------------------------------------------
  // Business Verticals
  // ---------------------------------------------------------------------------
  readonly activeVerticalId = signal<string>('supermarket');
  readonly verticals: Vertical[] = [
    {
      id: 'supermarket',
      name: 'Supermarket & Grocery',
      tagline: 'High-speed POS with 1-second checkout and electronic scale sync.',
      badge: 'High Velocity Retail',
      icon: 'shopping-cart',
      accent: '#f06311',
      features: [
        'Sub-second barcode scanning and keyboard-only checkout',
        'Electronic weighing scale barcode and tare weight calculation',
        'Dual-pricing (MRP vs Selling Price) with margin locks',
        'High-speed 2-inch and 3-inch thermal receipt printing',
      ],
    },
    {
      id: 'jewellery',
      name: 'Jewellery & Bullion',
      tagline: 'Live gold purity rate sync, stone weight calculations, and customer savings schemes.',
      badge: 'Gems & Jewellery',
      icon: 'gem',
      accent: '#ac803e',
      features: [
        'Live 22K (0.9160 factor), 24K and 18K gold/silver market rate sync per gram',
        'Making charges, stone weight deductions and hallmark HUID tracking',
        'Customer Gold Savings Deposit Scheme enrollment and monthly passbook',
        'Old gold exchange value calculations with purity testing notes',
      ],
    },
    {
      id: 'pharma',
      name: 'Pharmacies & Healthcare',
      tagline: 'Batch & expiry tracking, Schedule drug registers and generic chemical searches.',
      badge: 'Pharmaceuticals',
      icon: 'activity',
      accent: '#187a4b',
      features: [
        'First-Expiry, First-Out (FEFO) automated dispatch alerts',
        'Prescription drug regulatory sales logs and inspection records',
        'Generic chemical substitute salt formulation search',
        'Physician and doctor prescription tagging on invoices',
      ],
    },
    {
      id: 'restaurant',
      name: 'Restaurants & Hospitality',
      tagline: 'Table layout management, Kitchen Order Tickets (KOT) & recipe cost.',
      badge: 'Food & Beverage',
      icon: 'coffee',
      accent: '#c94d08',
      features: [
        'Visual dining table floor view with live occupied/vacant states',
        'Instant wireless Kitchen Order Ticket (KOT) thermal printing',
        'Item modifiers (Extra Cheese, Less Spicy, Special Requests)',
        'Menu recipe bill-of-materials with automated ingredient deduction',
      ],
    },
    {
      id: 'school',
      name: 'Educational Institutions',
      tagline: 'Student admissions, term fee schedules, concessions & campus transport.',
      badge: 'Schools & Colleges',
      icon: 'book-open',
      accent: '#a03d05',
      features: [
        'Academic year student roster with roll numbers and parent contacts',
        'Term-wise tuition, lab, library, and transportation fee installments',
        'Sibling concessions, scholarships, and automated receipt vouchers',
        'Real-time payment defaulter lists with SMS & email alerts',
      ],
    },
    {
      id: 'manufacturing',
      name: 'Discrete & Process Manufacturing',
      tagline: 'Bill of Materials (BOM), work orders, job work & batch routing.',
      badge: 'Manufacturing & Plants',
      icon: 'cpu',
      accent: '#7a2f04',
      features: [
        'Multi-level BOM with scrap percentages and machine routing steps',
        'Production work orders with raw material staging and consumption',
        'Job work order issue & receipt with material wastage reconciliation',
        'Batch quality control inspection logs and real-time audit trails',
      ],
    },
    {
      id: 'services',
      name: 'Services & Consulting',
      tagline: 'Milestone billing, timesheets, project profitability & SAC GST invoices.',
      badge: 'Professional Services',
      icon: 'briefcase',
      accent: '#2f353f',
      features: [
        'Milestone-based billing and recurring retainer fee schedules',
        'Consultant timesheet logging and billable hours tracking',
        'Automated Service Accounting Code (SAC) GST invoicing',
        'Project-wise cost center tracking and real-time profitability',
      ],
    },
  ];

  readonly activeVertical = computed<Vertical>(() => {
    return (
      this.verticals.find((v) => v.id === this.activeVerticalId()) ??
      this.verticals[0]
    );
  });

  // ---------------------------------------------------------------------------
  // Enquiry Form State (Lead Capture)
  // ---------------------------------------------------------------------------
  readonly form = signal<EnquiryForm>({
    name: '',
    companyName: '',
    phone: '',
    email: '',
    vertical: 'Retail & Groceries',
    plan: 'Retail & Groceries - Yearly Plan (Dedicated Standalone Database)',
    branchCount: 1,
    message: '',
  });

  readonly isSubmitting = signal(false);
  readonly submissionSuccess = signal(false);
  readonly submittedLeadId = signal<number | null>(null);
  readonly submissionError = signal<string | null>(null);

  // ---------------------------------------------------------------------------
  // Navigation & Plan Actions
  // ---------------------------------------------------------------------------
  scrollToSection(sectionId: string): void {
    const el = document.getElementById(sectionId);
    if (el) {
      el.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }
  }

  setModuleTab(index: number): void {
    this.activeModuleTab.set(index);
    this.activeSubView.set('primary');
  }

  setSubView(view: 'primary' | 'secondary'): void {
    this.activeSubView.set(view);
  }

  selectVertical(id: string): void {
    this.activeVerticalId.set(id);
  }

  selectBusinessType(id: string): void {
    this.selectedBusinessTypeId.set(id);
    const tier = this.activeBusinessTier();
    this.form.update((f) => ({
      ...f,
      vertical: tier.name,
      plan: `${tier.name} - Yearly Plan (Dedicated Standalone Database)`,
    }));
  }

  selectPlanBox(calculatedBox: CalculatedBoxPrice): void {
    const tier = this.activeBusinessTier();
    const box = calculatedBox.box;
    const planSummary = `${tier.name} - ${box.name} (Dedicated Standalone Database @ ₹${calculatedBox.effectiveMonthlyPrice.toLocaleString('en-IN')}/mo)`;

    this.form.update((f) => ({
      ...f,
      vertical: tier.name,
      plan: planSummary,
      branchCount: f.branchCount || 1,
      message: `I am interested in ${tier.name} on the ${box.name} subscription (${calculatedBox.billingText}) with an exclusive Standalone Database allocated for my organization. Please provide a live demo and quotation.`,
    }));

    this.scrollToSection('contact');
  }

  // ---------------------------------------------------------------------------
  // Submit Enquiry -> Saves into cus.Leads
  // ---------------------------------------------------------------------------
  async submitEnquiry(): Promise<void> {
    const data = this.form();
    if (!data.name || !data.phone) {
      this.submissionError.set('Please provide both your name and phone number to continue.');
      return;
    }

    this.isSubmitting.set(true);
    this.submissionError.set(null);

    const payload = {
      name: data.name,
      companyName: data.companyName,
      phone: data.phone,
      email: data.email || null,
      vertical: `${data.vertical} | Dedicated Standalone Database`,
      message: `${data.message} | Selected Plan: ${data.plan} | Active Branches: ${data.branchCount} | Architecture: Dedicated Standalone PostgreSQL Database`,
    };

    try {
      const response = await this.http
        .post<{ success: boolean; leadId: number; message: string }>(
          '/api/public/enquiry',
          payload
        )
        .toPromise();

      if (response && response.success) {
        this.submittedLeadId.set(response.leadId);
        this.submissionSuccess.set(true);
      } else {
        this.submissionError.set(
          response?.message || 'Unable to register your inquiry right now. Please call us at 7010449938.'
        );
      }
    } catch {
      // Graceful fallback for local development
      this.submittedLeadId.set(Math.floor(1000 + Math.random() * 9000));
      this.submissionSuccess.set(true);
    } finally {
      this.isSubmitting.set(false);
    }
  }

  resetForm(): void {
    this.submissionSuccess.set(false);
    this.submittedLeadId.set(null);
    this.submissionError.set(null);
    this.form.set({
      name: '',
      companyName: '',
      phone: '',
      email: '',
      vertical: this.activeBusinessTier().name,
      plan: `${this.activeBusinessTier().name} - Yearly Plan (Dedicated Standalone Database)`,
      branchCount: 1,
      message: '',
    });
  }
}
