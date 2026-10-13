import {
  AfterViewInit,
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  OnInit,
  computed,
  inject,
  signal,
  viewChild,
  viewChildren,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { ActivatedRoute, Router } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { readApiFailure } from '@bill-book/api-client';
import { SessionContextService } from '@bill-book/auth';
import {
  DocumentTypeOption,
  PaperSize,
  PlaceholderGroup,
  PrintContent,
  PrintSettings,
  PrintTemplateDetail,
  PrintTemplateListItem,
  PrintTemplateService,
  PrinterType,
  SegmentMargins,
  SegmentPosition,
} from '@bill-book/master-core';
import {
  ConfirmDialogService,
  CustomFieldDefinition,
  MessageBoxComponent,
  SelectComponent,
  UiMessage,
} from '@bill-book/ui-components';

export const MM_PX = 3.7795; // 1 mm = 3.7795 px shared constant

export const PAPER_SIZES: Record<PaperSize, [number, number]> = {
  A4: [210, 297],
  A5: [148, 210],
  Letter: [216, 279],
  Legal: [216, 356],
};

export type SegCode = 'fh' | 'h' | 'd' | 'f' | 'ff';

export interface SegmentDef {
  code: SegCode;
  label: string;
  backendKey: keyof SegmentMargins;
  contentKey: keyof PrintContent;
}

export const SEGMENTS: SegmentDef[] = [
  { code: 'fh', label: 'Fixed header', backendKey: 'fixedHeader', contentKey: 'fixedHeaderHtml' },
  { code: 'h', label: 'Header', backendKey: 'header', contentKey: 'headerHtml' },
  { code: 'd', label: 'Details', backendKey: 'details', contentKey: 'detailsHtml' },
  { code: 'f', label: 'Footer', backendKey: 'footer', contentKey: 'footerHtml' },
  { code: 'ff', label: 'Fixed footer', backendKey: 'fixedFooter', contentKey: 'fixedFooterHtml' },
];

const CUSTOM_FIELD_KINDS: Record<string, string> = {
  QTE: 'Quote',
  SOR: 'SalesOrder',
  DLC: 'DeliveryChallan',
  INV: 'Invoice',
  POS: 'Invoice',
  CRN: 'CreditNote',
  POR: 'PurchaseOrder',
  GRN: 'GoodsReceipt',
  BIL: 'Bill',
  DBN: 'DebitNote',
};

const MOCK_ROWS: Record<string, number> = { Item: 42, Tax: 3, Payment: 2, Alloc: 3, Line: 6 };

const MOCK_SINGLE: Record<string, string> = {
  'Organization.Logo': 'MERIDIAN',
  'Organization.Name': 'Meridian Trading Co.',
  'Organization.Address': '14 Fort Road, Ballard Estate, Mumbai 400001',
  'Organization.GSTIN': '27AABCM1234K1Z9',
  'Organization.Phone': '+91 22 4021 8890',
  InvoiceNo: 'SI/2026/00418',
  InvoiceDate: '06-Sep-2026',
  DueDate: '21-Sep-2026',
  'Customer.Name': 'Halcyon Interiors Pvt Ltd',
  'Customer.Address': '3rd Floor, Prestige Atrium, Residency Road, Bengaluru 560025',
  'Customer.GSTIN': '29AACCH8821P1ZB',
  PaymentTerms: 'Net 15 days from invoice date',
  PONumber: 'PO/2026/00219',
  PODate: '02-Sep-2026',
  'Vendor.Name': 'Sundar Steel Works',
  'Vendor.Address': 'Plot 42, MIDC Phase II, Pune 411026',
  'Vendor.GSTIN': '27AAFCS4471M1Z2',
  DeliveryDate: '18-Sep-2026',
  ChallanNo: 'DC/2026/00731',
  ChallanDate: '06-Sep-2026',
  VehicleNo: 'MH 12 QT 4417',
  DriverName: 'R. Anbarasan',
  CreditNoteNo: 'CN/2026/00042',
  CreditNoteDate: '06-Sep-2026',
  AgainstInvoiceNo: 'SI/2026/00311',
  Reason: 'Short supply against original invoice',
  PurchaseOrderNo: 'PO/2026/00219',
  PurchaseOrderDate: '02-Sep-2026',
  QuoteNo: 'QOT/2026/00319',
  QuoteDate: '01-Sep-2026',
  SalesOrderNo: 'SOR/2026/00943',
  SalesOrderDate: '02-Sep-2026',
  DeliveryChallanNo: 'DC/2026/00775',
  DeliveryChallanDate: '05-Sep-2026',
  GoodsReceiptNo: 'GRN/2026/00589',
  GoodsReceiptDate: '05-Sep-2026',
  BillNo: 'BIL/2026/01207',
  BillDate: '03-Sep-2026',
  DebitNoteNo: 'DBN/2026/00098',
  DebitNoteDate: '06-Sep-2026',
  ReceiptNo: 'REC/2026/00812',
  ReceiptDate: '06-Sep-2026',
  PaymentNo: 'PAY/2026/00477',
  PaymentDate: '06-Sep-2026',
  JournalNo: 'JV/2526/00319',
  JournalDate: '06-Sep-2026',
  Narration: 'Being depreciation, bank charges and rent for September 2026',
  PaymentMode: 'Bank transfer',
  PaymentReference: 'UTR 402188173',
  AgainstDocNo: 'SI/2026/00311',
  Subtotal: '3,42,180.00',
  TotalTax: '61,592.40',
  RoundOff: '(0.40)',
  TotalAmount: '4,03,772.00',
  BalanceDue: '2,53,772.00',
  AmountInWords: 'Rupees Four Lakh Three Thousand Seven Hundred Seventy Two only',
};

const MOCK_ITEMS: [string, string, string, string, string, string][] = [
  ['Mild steel angle 50×50×6 mm', '7216', '120.000', 'kg', '78.50', '9,420.00'],
  ['MS flat bar 40×6 mm', '7214', '86.000', 'kg', '74.20', '6,381.20'],
  ['Galvanised sheet 1.6 mm', '7210', '24.000', 'sheet', '1,180.00', '28,320.00'],
  ['SS 304 pipe 25 mm', '7306', '48.000', 'm', '512.00', '24,576.00'],
  ['Powder coating — matte black', '9988', '160.000', 'sqft', '46.00', '7,360.00'],
  ['Hex bolt M10×40 zinc', '7318', '400.000', 'nos', '11.40', '4,560.00'],
  ['Welding consumables', '8311', '18.000', 'kg', '268.00', '4,824.00'],
  ['Fabrication labour', '9987', '64.000', 'hr', '340.00', '21,760.00'],
  ['Site installation charges', '9954', '1.000', 'lot', '18,500.00', '18,500.00'],
  ['Transport — Mumbai to Bengaluru', '9965', '1.000', 'trip', '14,200.00', '14,200.00'],
];

const MOCK_LIST: Record<string, ((i: number) => string) | string[]> = {
  'Item.SlNo': (i: number) => String(i + 1),
  'Item.ItemName': (i: number) => MOCK_ITEMS[i % MOCK_ITEMS.length][0],
  'Item.HSN': (i: number) => MOCK_ITEMS[i % MOCK_ITEMS.length][1],
  'Item.Qty': (i: number) => MOCK_ITEMS[i % MOCK_ITEMS.length][2],
  'Item.Unit': (i: number) => MOCK_ITEMS[i % MOCK_ITEMS.length][3],
  'Item.Rate': (i: number) => MOCK_ITEMS[i % MOCK_ITEMS.length][4],
  'Item.Amount': (i: number) => MOCK_ITEMS[i % MOCK_ITEMS.length][5],
  'Tax.Name': ['CGST', 'SGST', 'IGST'],
  'Tax.Rate': ['9.00', '9.00', '18.00'],
  'Tax.TaxableValue': ['1,71,090.00', '1,71,090.00', '1,71,090.00'],
  'Tax.Amount': ['15,398.10', '15,398.10', '30,796.20'],
  'Payment.Date': ['28-Aug-2026', '04-Sep-2026'],
  'Payment.Mode': ['Bank transfer', 'UPI'],
  'Payment.Reference': ['UTR 402188173', 'UPI 6620041188'],
  'Payment.Amount': ['1,00,000.00', '50,000.00'],
  'Alloc.DocNo': ['SI/2026/00311', 'SI/2026/00344', 'SI/2026/00418'],
  'Alloc.Date': ['12-Aug-2026', '24-Aug-2026', '06-Sep-2026'],
  'Alloc.Amount': ['84,200.00', '1,12,400.00', '53,400.00'],
  'Alloc.Balance': ['0.00', '0.00', '2,53,772.00'],
  'Line.Account': [
    'Depreciation',
    'Accumulated depreciation',
    'Bank charges',
    'Bank — HDFC 4021',
    'Rent',
    'Landlord payable',
  ],
  'Line.Narration': [
    'Monthly charge',
    'Monthly charge',
    'September fees',
    'September fees',
    'September rent',
    'September rent',
  ],
  'Line.Debit': ['18,400.00', '', '1,180.00', '', '65,000.00', ''],
  'Line.Credit': ['', '18,400.00', '', '1,180.00', '', '65,000.00'],
};

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-print-templates-page',
  standalone: true,
  imports: [
    MessageBoxComponent,
    SelectComponent,
    FormsModule,
  ],
  templateUrl: './print-templates.page.html',
  styleUrl: './print-templates.page.scss',
})
export class PrintTemplatesPage implements OnInit, AfterViewInit {
  private readonly api = inject(PrintTemplateService);
  private readonly http = inject(HttpClient);
  private readonly session = inject(SessionContextService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly sanitizer = inject(DomSanitizer);
  private readonly destroyRef = inject(DestroyRef);
  private readonly confirmDialog = inject(ConfirmDialogService);

  protected readonly segElements = viewChildren<ElementRef<HTMLDivElement>>('segContent');
  protected readonly paperScroller = viewChild<ElementRef<HTMLDivElement>>('paperScroller');
  protected readonly flowMeasure = viewChild<ElementRef<HTMLDivElement>>('flowMeasure');
  protected readonly fhMeasure = viewChild<ElementRef<HTMLDivElement>>('fhMeasure');
  protected readonly ffMeasure = viewChild<ElementRef<HTMLDivElement>>('ffMeasure');
  protected readonly footerMeasure = viewChild<ElementRef<HTMLDivElement>>('footerMeasure');

  protected readonly segments = SEGMENTS;
  protected readonly canEdit = computed(() => this.session.has('settings.edit'));

  // Document types & Templates
  protected readonly documentTypes = signal<DocumentTypeOption[]>([]);
  protected readonly typeOptions = computed(() =>
    this.documentTypes().map((type) => ({ value: type.code, label: type.name })),
  );
  protected readonly documentType = signal<string | null>(null);
  protected readonly templates = signal<PrintTemplateListItem[]>([]);
  protected readonly placeholders = signal<PlaceholderGroup[]>([]);
  protected readonly detail = signal<PrintTemplateDetail | null>(null);

  // Template settings & draft
  protected readonly draftName = signal('');
  protected readonly draftContent = signal<PrintContent | null>(null);
  protected readonly draftSettings = signal<PrintSettings>({
    printerType: 'laser',
    paperSize: 'A4',
    rollWidthMm: 80,
    rollUnit: 'mm',
    marginTopMm: 16,
    marginBottomMm: 16,
    marginLeftMm: 18,
    marginRightMm: 18,
    footerPos: 'inline',
    fixedFooterPos: 'inline',
    fixedFooterPref: 'inline',
    segMargins: {
      fixedHeader: { aboveMm: 0, belowMm: 2 },
      header: { aboveMm: 2, belowMm: 2 },
      details: { aboveMm: 2, belowMm: 2 },
      footer: { aboveMm: 2, belowMm: 0 },
      fixedFooter: { aboveMm: 2, belowMm: 0 },
    },
  });

  // Editor states
  protected readonly activeSeg = signal<SegCode>('d');
  protected readonly mode = signal<'edit' | 'preview'>('edit');
  protected readonly zoom = signal<number>(1);
  protected readonly zoomStr = signal<string>('1');
  protected readonly flash = signal<string | null>(null);
  protected readonly busy = signal(false);
  protected readonly messages = signal<UiMessage[]>([]);

  // Selection & Formatting states
  protected readonly ptFmt = signal<{
    b: boolean;
    i: boolean;
    u: boolean;
    s: boolean;
    l: boolean;
    c: boolean;
    r: boolean;
    j: boolean;
    ul: boolean;
    ol: boolean;
  }>({
    b: false,
    i: false,
    u: false,
    s: false,
    l: false,
    c: false,
    r: false,
    j: false,
    ul: false,
    ol: false,
  });
  protected readonly foreColor = signal('#201f1d');
  protected readonly hiliteColor = signal('#fff3a0');
  protected readonly lastFont = signal('Arial');
  private activeRange: Range | null = null;
  private activeCell: HTMLElement | null = null;
  private flashTimer: ReturnType<typeof setTimeout> | null = null;

  // Insert field popover
  protected readonly fieldsOpen = signal(false);
  protected readonly fieldsPos = signal<{ left: number; top: number }>({ left: 0, top: 0 });

  // Preview Pagination State
  protected readonly previewPage = signal(1);
  protected readonly previewPageCount = signal(1);
  protected readonly starts = signal<number[]>([0]);
  protected readonly previewHtmlMap = signal<Record<SegCode, SafeHtml>>({
    fh: '',
    h: '',
    d: '',
    f: '',
    ff: '',
  });

  // Fonts & Swatches
  protected readonly fontFamilies = [
    'Arial',
    'Arial Black',
    'Calibri',
    'Cambria',
    'Candara',
    'Comic Sans MS',
    'Consolas',
    'Constantia',
    'Corbel',
    'Courier New',
    'Franklin Gothic Medium',
    'Garamond',
    'Georgia',
    'Helvetica',
    'Impact',
    'Lucida Console',
    'Lucida Sans Unicode',
    'Palatino Linotype',
    'Segoe UI',
    'Tahoma',
    'Times New Roman',
    'Trebuchet MS',
    'Verdana',
  ];
  protected readonly fontSizes = [8, 9, 10, 11, 12, 14, 16, 18, 20, 22, 24, 28, 32, 36, 48, 72];
  protected readonly foreSwatches = ['#201f1d', '#b03a2e', '#1e6f50', '#2050a8', '#8a6d3b', '#6b4fa0'];
  protected readonly hiliteSwatches = [
    '#fff3a0',
    '#ffd6a5',
    '#c8f5d0',
    '#c9e4ff',
    '#f0d0ff',
    '#ffffff',
  ];

  // Computed layout and dirty checking
  protected readonly dirty = computed(() => {
    const detail = this.detail();
    const draft = this.draftContent();
    const settings = this.draftSettings();
    if (!detail || !draft || !settings) {
      return false;
    }
    if (this.draftName().trim() !== detail.templateName) {
      return true;
    }
    if (JSON.stringify(draft) !== JSON.stringify(detail.content)) {
      return true;
    }
    if (JSON.stringify(settings) !== JSON.stringify(detail.settings)) {
      return true;
    }
    return false;
  });

  protected readonly documentTypeName = computed(
    () =>
      this.documentTypes().find((type) => type.code === this.documentType())?.name ??
      this.documentType() ??
      '',
  );

  protected readonly isLaser = computed(() => this.draftSettings().printerType !== 'thermal');
  protected readonly paperSize = computed(() => this.draftSettings().paperSize ?? 'A4');
  protected readonly rollWidthMm = computed(() => this.draftSettings().rollWidthMm ?? 80);
  protected readonly rollUnit = computed(() => this.draftSettings().rollUnit ?? 'mm');

  protected readonly padTop = computed(() =>
    Math.round((this.draftSettings().marginTopMm ?? 16) * MM_PX),
  );
  protected readonly padBottom = computed(() =>
    Math.round((this.draftSettings().marginBottomMm ?? 16) * MM_PX),
  );
  protected readonly padLeft = computed(() =>
    Math.round((this.draftSettings().marginLeftMm ?? 18) * MM_PX),
  );
  protected readonly padRight = computed(() =>
    Math.round((this.draftSettings().marginRightMm ?? 18) * MM_PX),
  );

  protected readonly pageWidthPx = computed(() => {
    const isLaser = this.isLaser();
    const pSize = this.paperSize();
    const [w] = PAPER_SIZES[pSize] ?? [210, 297];
    return Math.round((isLaser ? w : this.rollWidthMm()) * MM_PX);
  });

  protected readonly pageHeightPx = computed(() => {
    if (!this.isLaser()) {
      return null;
    }
    const pSize = this.paperSize();
    const [, h] = PAPER_SIZES[pSize] ?? [210, 297];
    return Math.round(h * MM_PX);
  });

  protected readonly canvasHeight = computed(() => {
    const h = this.pageHeightPx();
    return h ? `${h}px` : 'auto';
  });

  protected readonly canvasPadding = computed(
    () =>
      `${this.padTop()}px ${this.padRight()}px ${this.padBottom()}px ${this.padLeft()}px`,
  );

  protected readonly zoomScale = computed(() => `scale(${this.zoom()})`);
  protected readonly zoomWidth = computed(() => `${Math.round(this.pageWidthPx() * this.zoom())}px`);
  protected readonly zoomHeight = computed(() => {
    const h = this.pageHeightPx();
    return h ? `${Math.round(h * this.zoom())}px` : 'auto';
  });

  protected readonly footerInline = computed(() => this.draftSettings().footerPos === 'inline');
  protected readonly fixedFooterInline = computed(() => {
    const footerPos = this.draftSettings().footerPos;
    const ffPos = footerPos === 'bottom' ? 'bottom' : this.draftSettings().fixedFooterPos;
    return ffPos === 'inline';
  });
  protected readonly ffLocked = computed(() => this.draftSettings().footerPos === 'bottom');

  protected readonly activeSegLabel = computed(() => {
    const seg = SEGMENTS.find((s) => s.code === this.activeSeg());
    return seg ? seg.label : 'Segment';
  });

  protected readonly activeSegTopMm = computed(() => {
    const seg = SEGMENTS.find((s) => s.code === this.activeSeg());
    if (!seg) return 0;
    const m = this.draftSettings().segMargins?.[seg.backendKey];
    return m?.aboveMm ?? 0;
  });

  protected readonly activeSegBottomMm = computed(() => {
    const seg = SEGMENTS.find((s) => s.code === this.activeSeg());
    if (!seg) return 0;
    const m = this.draftSettings().segMargins?.[seg.backendKey];
    return m?.belowMm ?? 0;
  });

  ngOnInit(): void {
    void this.start();
  }

  ngAfterViewInit(): void {
    // Content will be populated when detail loads
  }

  private async start(): Promise<void> {
    try {
      this.documentTypes.set(await this.api.documentTypes());
    } catch (error) {
      this.fail(error);
      return;
    }

    this.route.paramMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((params) => {
      const requested = (params.get('docType') ?? '').toUpperCase();
      const known = this.documentTypes().some((type) => type.code === requested);
      void this.openType(known ? requested : (this.documentTypes()[0]?.code ?? null));
    });
  }

  protected onTypeChange(code: string): void {
    void this.router.navigate(['/settings/print-templates', code]);
  }

  private async openType(code: string | null): Promise<void> {
    this.documentType.set(code);
    this.detail.set(null);
    this.draftContent.set(null);
    this.templates.set([]);
    this.placeholders.set([]);
    this.messages.set([]);
    this.mode.set('edit');

    if (code === null) {
      return;
    }

    try {
      const [templates, placeholders] = await Promise.all([
        this.api.list(code),
        this.api.placeholders(code),
      ]);

      if (this.documentType() !== code) {
        return;
      }

      this.templates.set(templates);
      this.placeholders.set([...placeholders, ...(await this.customPlaceholders(code))]);

      const first = templates.find((t) => t.isDefault) ?? templates[0];
      if (first) {
        await this.select(first.printTemplateId);
      }
    } catch (error) {
      this.fail(error);
    }
  }

  protected async select(id: number): Promise<void> {
    if (this.dirty()) {
      const ok = await this.confirmDialog.confirm({
        title: 'Discard changes?',
        message: 'Discard the changes you have not saved?',
        confirmLabel: 'Discard',
        tone: 'danger',
      });
      if (!ok) {
        return;
      }
    }

    try {
      this.apply(await this.api.get(id));
    } catch (error) {
      this.fail(error);
    }
  }

  private apply(detail: PrintTemplateDetail): void {
    this.detail.set(detail);
    this.draftName.set(detail.templateName);
    this.draftContent.set({ ...detail.content });

    const s = detail.settings;
    this.draftSettings.set({
      printerType: s.printerType ?? 'laser',
      paperSize: s.paperSize ?? 'A4',
      rollWidthMm: s.rollWidthMm ?? 80,
      rollUnit: s.rollUnit ?? 'mm',
      marginTopMm: s.marginTopMm ?? 16,
      marginBottomMm: s.marginBottomMm ?? 16,
      marginLeftMm: s.marginLeftMm ?? 18,
      marginRightMm: s.marginRightMm ?? 18,
      footerPos: s.footerPos ?? 'inline',
      fixedFooterPos: s.fixedFooterPos ?? 'inline',
      fixedFooterPref: s.fixedFooterPref ?? 'inline',
      segMargins: {
        fixedHeader: {
          aboveMm: s.segMargins?.fixedHeader?.aboveMm ?? 0,
          belowMm: s.segMargins?.fixedHeader?.belowMm ?? 2,
        },
        header: {
          aboveMm: s.segMargins?.header?.aboveMm ?? 2,
          belowMm: s.segMargins?.header?.belowMm ?? 2,
        },
        details: {
          aboveMm: s.segMargins?.details?.aboveMm ?? 2,
          belowMm: s.segMargins?.details?.belowMm ?? 2,
        },
        footer: {
          aboveMm: s.segMargins?.footer?.aboveMm ?? 2,
          belowMm: s.segMargins?.footer?.belowMm ?? 0,
        },
        fixedFooter: {
          aboveMm: s.segMargins?.fixedFooter?.aboveMm ?? 2,
          belowMm: s.segMargins?.fixedFooter?.belowMm ?? 0,
        },
      },
    });

    this.messages.set(
      detail.unknownTags.length > 0
        ? [
            {
              tone: 'warning',
              text: 'Some fields in this template do not exist for this document type. They print as nothing.',
              detail: detail.unknownTags,
            },
          ]
        : [],
    );

    this.loadContentIntoDom(detail.content);
  }

  private loadContentIntoDom(content: PrintContent): void {
    setTimeout(() => {
      for (const ref of this.segElements()) {
        const code = ref.nativeElement.dataset['seg'] as SegCode;
        if (!code) continue;
        const segDef = SEGMENTS.find((s) => s.code === code);
        if (!segDef) continue;
        const html = content[segDef.contentKey] || '';
        ref.nativeElement.innerHTML = this.tagToChip(html);
      }
    });
  }

  // ---- Tag <-> Chip conversions ---------------------------------------------

  protected tagToChip(html: string): string {
    if (!html) return '';
    return html.replace(/\{\{([^{}]+)\}\}/g, (_match, rawTag) => {
      const tag = rawTag.trim();
      const isList = this.isListTag(tag);
      return `<span contenteditable="false" class="pt-chip">${isList ? '↻ ' : ''}«${tag}»</span>\u00a0`;
    });
  }

  protected chipToTag(html: string): string {
    if (!html) return '';
    const div = document.createElement('div');
    div.innerHTML = html;
    const chips = div.querySelectorAll('.pt-chip');
    chips.forEach((chip) => {
      const raw = chip.textContent || '';
      const cleanTag = raw.replace(/[↻\s«»]/g, '').trim();
      const tagNode = document.createTextNode(`{{${cleanTag}}}`);
      chip.replaceWith(tagNode);
    });
    return div.innerHTML;
  }

  private isListTag(tag: string): boolean {
    for (const group of this.placeholders()) {
      if (group.isList && group.placeholders.some((p) => p.tag.toLowerCase() === tag.toLowerCase())) {
        return true;
      }
    }
    return false;
  }

  protected getSegmentTagHtml(code: SegCode): string {
    const element = this.segElements().find(
      (ref) => ref.nativeElement.dataset['seg'] === code,
    )?.nativeElement;
    if (element) {
      return this.chipToTag(element.innerHTML);
    }
    const segDef = SEGMENTS.find((s) => s.code === code);
    return segDef && this.draftContent() ? this.draftContent()![segDef.contentKey] : '';
  }

  // ---- Settings mutations ---------------------------------------------------

  protected setPrinterType(type: PrinterType): void {
    this.draftSettings.update((s) => ({ ...s, printerType: type }));
  }

  protected setPaperSize(size: PaperSize): void {
    this.draftSettings.update((s) => ({ ...s, paperSize: size }));
  }

  protected setRollWidth(width: number): void {
    this.draftSettings.update((s) => ({ ...s, rollWidthMm: width }));
  }

  protected setRollUnit(unit: 'mm' | 'in'): void {
    this.draftSettings.update((s) => ({ ...s, rollUnit: unit }));
  }

  protected setMargin(side: 'Top' | 'Bottom' | 'Left' | 'Right', val: number): void {
    const key = `margin${side}Mm` as keyof PrintSettings;
    this.draftSettings.update((s) => ({ ...s, [key]: val }));
  }

  protected setSegMargin(aboveOrBelow: 'above' | 'below', val: number): void {
    const seg = SEGMENTS.find((s) => s.code === this.activeSeg());
    if (!seg) return;
    this.draftSettings.update((s) => {
      const curMargins = s.segMargins ?? {
        fixedHeader: { aboveMm: 0, belowMm: 2 },
        header: { aboveMm: 2, belowMm: 2 },
        details: { aboveMm: 2, belowMm: 2 },
        footer: { aboveMm: 2, belowMm: 0 },
        fixedFooter: { aboveMm: 2, belowMm: 0 },
      };
      const cur = curMargins[seg.backendKey] ?? { aboveMm: 0, belowMm: 0 };
      const nextMargins: SegmentMargins = {
        fixedHeader: { ...curMargins.fixedHeader },
        header: { ...curMargins.header },
        details: { ...curMargins.details },
        footer: { ...curMargins.footer },
        fixedFooter: { ...curMargins.fixedFooter },
        [seg.backendKey]: {
          aboveMm: aboveOrBelow === 'above' ? val : cur.aboveMm,
          belowMm: aboveOrBelow === 'below' ? val : cur.belowMm,
        },
      };
      return { ...s, segMargins: nextMargins };
    });
  }

  protected setFooterPos(pos: SegmentPosition): void {
    this.draftSettings.update((s) => ({
      ...s,
      footerPos: pos,
      fixedFooterPos: pos === 'bottom' ? 'bottom' : s.fixedFooterPref ?? 'inline',
    }));
  }

  protected setFixedFooterPos(pos: SegmentPosition): void {
    if (this.ffLocked()) return;
    this.draftSettings.update((s) => ({
      ...s,
      fixedFooterPos: pos,
      fixedFooterPref: pos,
    }));
  }

  protected getSegMarginStyle(code: SegCode): string {
    const seg = SEGMENTS.find((s) => s.code === code);
    if (!seg) return '0';
    const m = this.draftSettings().segMargins?.[seg.backendKey];
    const top = Math.round((m?.aboveMm ?? 0) * MM_PX);
    const bottom = Math.round((m?.belowMm ?? 0) * MM_PX);
    return `${top}px 0 ${bottom}px`;
  }

  protected isSegPinned(code: SegCode): boolean {
    if (code === 'f') return !this.footerInline();
    if (code === 'ff') return !this.fixedFooterInline();
    return false;
  }

  protected isSegPinFirst(code: SegCode): boolean {
    if (code === 'f') return !this.footerInline();
    if (code === 'ff') return !this.fixedFooterInline() && this.footerInline();
    return false;
  }

  // ---- Editor Interactions & Formatting ------------------------------------

  protected setActiveSeg(code: SegCode): void {
    this.activeSeg.set(code);
    setTimeout(() => {
      const el = this.segElements().find(
        (ref) => ref.nativeElement.dataset['seg'] === code,
      )?.nativeElement;
      if (el) {
        el.focus();
        this.onSnap();
      }
    });
  }

  protected onSegmentInput(code: SegCode): void {
    const segDef = SEGMENTS.find((s) => s.code === code);
    if (!segDef) return;
    const tagHtml = this.getSegmentTagHtml(code);
    const draft = this.draftContent();
    if (draft) {
      this.draftContent.set({ ...draft, [segDef.contentKey]: tagHtml });
    }
  }

  protected keepSel(e: MouseEvent): void {
    e.preventDefault();
  }

  protected onSnap(): void {
    const sel = window.getSelection();
    if (sel && sel.rangeCount > 0) {
      this.activeRange = sel.getRangeAt(0).cloneRange();
      let node: Node | null = sel.anchorNode;
      while (node && node.nodeType !== 1) {
        node = node.parentNode;
      }
      this.activeCell = node && (node as HTMLElement).closest ? (node as HTMLElement).closest('td,th') : null;
    }
    this.syncFmt();
  }

  private syncFmt(): void {
    const q = (c: string) => {
      try {
        return document.queryCommandState(c);
      } catch {
        return false;
      }
    };
    const f = {
      b: q('bold'),
      i: q('italic'),
      u: q('underline'),
      s: q('strikeThrough'),
      l: false,
      c: false,
      r: false,
      j: false,
      ul: q('insertUnorderedList'),
      ol: q('insertOrderedList'),
    };

    let el: HTMLElement | null = this.activeCell;
    if (!el) {
      const sel = window.getSelection();
      let n = sel && sel.anchorNode;
      while (n && n.nodeType !== 1) n = n.parentNode;
      el = n as HTMLElement | null;
    }
    if (el) {
      const ta = getComputedStyle(el).textAlign;
      f.c = ta === 'center';
      f.r = ta === 'right' || ta === 'end';
      f.j = ta === 'justify';
      f.l = !f.c && !f.r && !f.j;
    }
    this.ptFmt.set(f);
  }

  private execSel(cmd: string, val: string = ''): void {
    if (this.activeRange) {
      const sel = window.getSelection();
      if (sel) {
        sel.removeAllRanges();
        sel.addRange(this.activeRange);
      }
    }
    document.execCommand(cmd, false, val);
    this.onSnap();
    this.onSegmentInput(this.activeSeg());
  }

  protected doBold(): void {
    this.execSel('bold');
  }

  protected doItalic(): void {
    this.execSel('italic');
  }

  protected doUnderline(): void {
    this.execSel('underline');
  }

  protected doStrike(): void {
    this.execSel('strikeThrough');
  }

  protected doUl(): void {
    this.execSel('insertUnorderedList');
  }

  protected doOl(): void {
    this.execSel('insertOrderedList');
  }

  protected doIndent(): void {
    this.execSel('indent');
  }

  protected doOutdent(): void {
    this.execSel('outdent');
  }

  protected align(value: 'left' | 'center' | 'right' | 'justify'): void {
    if (this.activeRange) {
      const sel = window.getSelection();
      if (sel) {
        sel.removeAllRanges();
        sel.addRange(this.activeRange);
      }
    }
    const sel = window.getSelection();
    if (!sel || !sel.rangeCount) return;
    const range = sel.getRangeAt(0);
    const seg = this.segElements().find(
      (ref) => ref.nativeElement.dataset['seg'] === this.activeSeg(),
    )?.nativeElement;

    const blockOf = (n: Node | null): HTMLElement | null => {
      while (n && n.nodeType !== 1) n = n.parentNode;
      const el = n && (n as HTMLElement).closest ? (n as HTMLElement).closest('td,th,li,p,h1,h2,h3,div') : null;
      return el && seg && seg.contains(el) && el !== seg ? (el as HTMLElement) : seg || null;
    };

    let targets: HTMLElement[] = [];
    if (!range.collapsed) {
      const scope = blockOf(range.commonAncestorContainer) || seg;
      if (scope) {
        targets = Array.from(scope.querySelectorAll('td,th,li,p')).filter((el) =>
          range.intersectsNode(el),
        ) as HTMLElement[];
      }
    }
    if (!targets.length) {
      const b = blockOf(range.startContainer);
      if (b) targets = [b];
    }
    targets.forEach((el) => {
      el.style.textAlign = value;
      if (el.hasAttribute('align')) el.removeAttribute('align');
    });
    this.onSnap();
    this.onSegmentInput(this.activeSeg());
  }

  protected setFontFamily(font: string): void {
    this.lastFont.set(font);
    this.execSel('fontName', font);
  }

  protected setFontSize(px: number): void {
    this.execSel('fontSize', '7');
    const seg = this.segElements().find(
      (ref) => ref.nativeElement.dataset['seg'] === this.activeSeg(),
    )?.nativeElement;
    if (!seg) return;
    Array.from(seg.querySelectorAll('font[size="7"]')).forEach((f) => {
      const span = document.createElement('span');
      span.style.fontSize = `${px}px`;
      while (f.firstChild) span.appendChild(f.firstChild);
      f.parentNode?.replaceChild(span, f);
    });
    this.onSegmentInput(this.activeSeg());
  }

  protected applyPageFont(): void {
    const font = this.lastFont();
    for (const ref of this.segElements()) {
      ref.nativeElement.style.fontFamily = font;
    }
    this.onSegmentInput(this.activeSeg());
  }

  protected pickFore(color: string): void {
    this.foreColor.set(color);
    this.execSel('foreColor', color);
  }

  protected pickHilite(color: string): void {
    this.hiliteColor.set(color);
    this.execSel('hiliteColor', color);
  }

  // ---- Table Manipulation ---------------------------------------------------

  protected insertTable(): void {
    const cell = '<td style="border:1px solid #ccc;padding:4px 6px;font-size:11px">&nbsp;</td>';
    this.execSel(
      'insertHTML',
      `<table style="width:100%;border-collapse:collapse;margin:6px 0"><tr>${cell}${cell}</tr><tr>${cell}${cell}</tr></table>`,
    );
  }

  protected rowBelow(): void {
    const cell = this.activeCell;
    if (!cell) return;
    const row = cell.closest('tr');
    if (!row) return;
    const clone = row.cloneNode(true) as HTMLTableRowElement;
    Array.from(clone.children).forEach((c) => {
      c.innerHTML = '&nbsp;';
      c.removeAttribute('colspan');
    });
    row.parentNode?.insertBefore(clone, row.nextSibling);
    this.onSegmentInput(this.activeSeg());
  }

  protected colRight(): void {
    const cell = this.activeCell;
    if (!cell) return;
    const row = cell.closest('tr');
    const table = cell.closest('table');
    if (!row || !table) return;
    const idx = Array.from(row.children).indexOf(cell);
    Array.from(table.rows).forEach((r) => {
      const ref = r.children[idx];
      const c2 = document.createElement(ref && ref.tagName === 'TH' ? 'th' : 'td');
      c2.style.cssText = 'border:1px solid #ccc;padding:4px 6px;font-size:11px';
      c2.innerHTML = '&nbsp;';
      if (ref) r.insertBefore(c2, ref.nextSibling);
      else r.appendChild(c2);
    });
    this.onSegmentInput(this.activeSeg());
  }

  protected mergeCells(): void {
    const cell = this.activeCell as HTMLTableCellElement | null;
    if (!cell) return;
    const next = cell.nextElementSibling as HTMLTableCellElement | null;
    if (!next) return;
    cell.colSpan = (Number(cell.colSpan) || 1) + (Number(next.colSpan) || 1);
    cell.innerHTML += ` ${next.innerHTML}`;
    next.remove();
    this.onSegmentInput(this.activeSeg());
  }

  protected splitCell(): void {
    const cell = this.activeCell as HTMLTableCellElement | null;
    if (!cell) return;
    const span = Number(cell.colSpan) || 1;
    if (span < 2) {
      cell.rowSpan = 1;
      return;
    }
    cell.colSpan = 1;
    for (let i = 1; i < span; i += 1) {
      const c2 = document.createElement(cell.tagName === 'TH' ? 'th' : 'td');
      c2.style.cssText = cell.style.cssText;
      c2.innerHTML = '&nbsp;';
      cell.parentNode?.insertBefore(c2, cell.nextSibling);
    }
    this.onSegmentInput(this.activeSeg());
  }

  protected delRow(): void {
    const cell = this.activeCell;
    if (!cell) return;
    const row = cell.closest('tr');
    const table = cell.closest('table');
    if (!row) return;
    row.remove();
    this.activeCell = null;
    if (table && table.rows.length === 0) table.remove();
    this.onSegmentInput(this.activeSeg());
  }

  protected delCol(): void {
    const cell = this.activeCell;
    if (!cell) return;
    const row = cell.closest('tr');
    const table = cell.closest('table');
    if (!row || !table) return;
    const idx = Array.from(row.children).indexOf(cell);
    Array.from(table.rows).forEach((r) => {
      if (r.children[idx]) r.children[idx].remove();
    });
    this.activeCell = null;
    if (table.rows.length && table.rows[0].children.length === 0) table.remove();
    this.onSegmentInput(this.activeSeg());
  }

  protected delCell(): void {
    const cell = this.activeCell;
    if (!cell) return;
    cell.remove();
    this.activeCell = null;
    this.onSegmentInput(this.activeSeg());
  }

  protected toggleBorder(): void {
    const cell = this.activeCell;
    if (!cell) return;
    cell.style.border = cell.style.border === '1px solid #000' ? '1px solid #ccc' : '1px solid #000';
    this.onSegmentInput(this.activeSeg());
  }

  protected toggleShade(): void {
    const cell = this.activeCell;
    if (!cell) return;
    cell.style.background = cell.style.background ? '' : 'rgba(242,97,15,.12)';
    this.onSegmentInput(this.activeSeg());
  }

  protected setColWidth(pct: number): void {
    const cell = this.activeCell;
    if (!cell || !pct) return;
    const row = cell.closest('tr');
    const table = cell.closest('table');
    if (!row || !table) return;
    const idx = Array.from(row.children).indexOf(cell);
    Array.from(table.rows).forEach((r) => {
      if (r.children[idx]) (r.children[idx] as HTMLElement).style.width = `${pct}%`;
    });
    this.onSegmentInput(this.activeSeg());
  }

  protected setRowHeight(px: number): void {
    const cell = this.activeCell;
    if (!cell || !px) return;
    const row = cell.closest('tr');
    if (row) (row as HTMLElement).style.height = `${px}px`;
    this.onSegmentInput(this.activeSeg());
  }

  // ---- Field Insertion Popover ----------------------------------------------

  protected openFields(e: MouseEvent): void {
    const rect = (e.currentTarget as HTMLElement).getBoundingClientRect();
    const w = 640;
    const h = 420;
    const left = Math.max(8, Math.min(rect.left, window.innerWidth - w - 8));
    const top = Math.max(8, Math.min(rect.bottom + 6, window.innerHeight - h - 8));
    this.fieldsPos.set({ left, top });
    this.fieldsOpen.set(true);
  }

  protected closeFields(): void {
    this.fieldsOpen.set(false);
  }

  protected insertField(tag: string, isList: boolean): void {
    const chipHtml = `<span contenteditable="false" class="pt-chip">${isList ? '↻ ' : ''}«${tag}»</span>\u00a0`;
    this.execSel('insertHTML', chipHtml);
    this.fieldsOpen.set(false);
  }

  // ---- Zoom Controls --------------------------------------------------------

  protected onZoomChange(val: string): void {
    this.zoomStr.set(val);
    if (val === 'fit') {
      const scroller = this.paperScroller()?.nativeElement;
      const w = scroller ? scroller.clientWidth : 0;
      const pw = this.pageWidthPx();
      const fit = w ? Math.max(0.25, Math.round(((w - 24) / pw) * 100) / 100) : 1;
      this.zoom.set(fit);
    } else {
      this.zoom.set(Number(val) || 1);
    }
  }

  // ---- Preview & Pagination -------------------------------------------------

  protected togglePreview(): void {
    if (this.mode() === 'edit') {
      // Bank content
      this.bankContentFromDom();
      this.mode.set('preview');
      this.previewPage.set(1);
      this.updatePreviewHtml();
      setTimeout(() => this.measurePreview(), 50);
    } else {
      this.mode.set('edit');
      const draft = this.draftContent();
      if (draft) {
        this.loadContentIntoDom(draft);
      }
    }
  }

  private bankContentFromDom(): void {
    const updated: PrintContent = {
      fixedHeaderHtml: this.getSegmentTagHtml('fh'),
      headerHtml: this.getSegmentTagHtml('h'),
      detailsHtml: this.getSegmentTagHtml('d'),
      footerHtml: this.getSegmentTagHtml('f'),
      fixedFooterHtml: this.getSegmentTagHtml('ff'),
    };
    this.draftContent.set(updated);
  }

  private mockVal(tag: string, i: number): string {
    const v = MOCK_LIST[tag];
    if (v) return typeof v === 'function' ? v(i) : v[i % v.length];
    return MOCK_SINGLE[tag] != null ? MOCK_SINGLE[tag] : tag;
  }

  protected ptMockHtml(html: string): string {
    const root = document.createElement('div');
    root.innerHTML = html || '';
    const tagOf = (el: Element) => (el.textContent || '').replace(/[↻\s«»]/g, '');

    Array.from(root.querySelectorAll('tr')).forEach((tr) => {
      if (tr.querySelector('tr')) return;
      const listChip = Array.from(tr.querySelectorAll('.pt-chip')).find(
        (c) => (c.textContent || '').indexOf('↻') >= 0,
      );
      if (!listChip) return;
      const n = MOCK_ROWS[tagOf(listChip).split('.')[0]] || 1;
      tr.setAttribute('data-mock-i', '0');
      let anchor = tr;
      for (let i = 1; i < n; i++) {
        const c = tr.cloneNode(true) as HTMLTableRowElement;
        c.setAttribute('data-mock-i', String(i));
        anchor.insertAdjacentElement('afterend', c);
        anchor = c;
      }
    });

    Array.from(root.querySelectorAll('.pt-chip')).forEach((chip) => {
      const row = chip.closest('tr[data-mock-i]');
      const val = this.mockVal(tagOf(chip), row ? Number(row.getAttribute('data-mock-i')) : 0);
      chip.replaceWith(document.createTextNode(val));
    });

    return root.innerHTML;
  }

  private updatePreviewHtml(): void {
    const res: Record<SegCode, SafeHtml> = {
      fh: this.sanitizer.bypassSecurityTrustHtml(this.ptMockHtml(this.tagToChip(this.getSegmentTagHtml('fh')))),
      h: this.sanitizer.bypassSecurityTrustHtml(this.ptMockHtml(this.tagToChip(this.getSegmentTagHtml('h')))),
      d: this.sanitizer.bypassSecurityTrustHtml(this.ptMockHtml(this.tagToChip(this.getSegmentTagHtml('d')))),
      f: this.sanitizer.bypassSecurityTrustHtml(this.ptMockHtml(this.tagToChip(this.getSegmentTagHtml('f')))),
      ff: this.sanitizer.bypassSecurityTrustHtml(this.ptMockHtml(this.tagToChip(this.getSegmentTagHtml('ff')))),
    };
    this.previewHtmlMap.set(res);
  }

  protected mockHtml(code: SegCode): SafeHtml {
    return this.previewHtmlMap()[code] ?? '';
  }

  protected measurePreview(): void {
    const flowEl = this.flowMeasure()?.nativeElement;
    const fhEl = this.fhMeasure()?.nativeElement;
    const ffEl = this.ffMeasure()?.nativeElement;
    const footerEl = this.footerMeasure()?.nativeElement;
    if (!flowEl) return;

    const laser = this.isLaser();
    const flowH = flowEl.scrollHeight;
    const fhH = fhEl?.offsetHeight ?? 0;
    const ffH = ffEl?.offsetHeight ?? 0;
    const footerH = footerEl?.offsetHeight ?? 0;
    const footerPinned = !this.footerInline();
    const ffBottom = !this.fixedFooterInline();

    const paperH = this.pageHeightPx() ?? 1000;
    const paperInner = Math.max(1, paperH - this.padTop() - this.padBottom());

    const contLimit = (i: number) => Math.max(1, paperInner - (i > 0 ? fhH : 0) - ffH);
    const lastLimit = (i: number) =>
      Math.max(
        1,
        paperInner -
          (i > 0 ? fhH : 0) -
          (ffBottom ? ffH : 0) -
          (footerPinned ? footerH : 0),
      );

    const starts = [0];
    if (laser && flowH > lastLimit(0)) {
      const base = flowEl.getBoundingClientRect().top;
      const scale = flowEl.getBoundingClientRect().height / Math.max(1, flowEl.offsetHeight);
      const un = (v: number) => v / (scale || 1);
      const blocks: { top: number; bottom: number }[] = [];

      Array.from(flowEl.children).forEach((child) => {
        const rows = Array.from(child.querySelectorAll('tr')).filter((tr) => !tr.querySelector('tr'));
        const parts = rows.length ? rows : [child];
        parts.forEach((el) => {
          const r = el.getBoundingClientRect();
          blocks.push({ top: un(r.top - base), bottom: un(r.bottom - base) });
        });
      });

      blocks.sort((x, y) => x.top - y.top);
      let cur = 0;
      let i = 0;

      while (i < 300 && flowH - cur > lastLimit(i)) {
        const lim = contLimit(i);
        let next: number | null = null;
        for (let k = 0; k < blocks.length; k++) {
          const b = blocks[k];
          if (b.top > cur && b.bottom - cur > lim) {
            next = b.top;
            break;
          }
        }
        if (next === null || next <= cur) break;
        cur = next;
        starts.push(cur);
        i++;
      }
    }

    this.starts.set(starts);
    this.previewPageCount.set(Math.max(1, starts.length));
  }

  protected prevPage(): void {
    this.previewPage.update((p) => Math.max(1, p - 1));
  }

  protected nextPage(): void {
    this.previewPage.update((p) => Math.min(this.previewPageCount(), p + 1));
  }

  protected isLastPage(): boolean {
    return this.previewPage() >= this.previewPageCount();
  }

  protected flowShift(): string {
    const start = this.starts()[this.previewPage() - 1] ?? 0;
    return `translateY(-${start}px)`;
  }

  protected flowTop(): string {
    const repeatFhH = this.fhMeasure()?.nativeElement.offsetHeight ?? 0;
    const top = this.padTop() + (this.isLaser() && this.previewPage() > 1 ? repeatFhH : 0);
    return `${top}px`;
  }

  protected flowBottom(): string {
    const ffH = this.ffMeasure()?.nativeElement.offsetHeight ?? 0;
    const footerH = this.footerMeasure()?.nativeElement.offsetHeight ?? 0;
    const bottom =
      this.padBottom() +
      (!this.fixedFooterInline() ? ffH : 0) +
      (this.isLastPage() && !this.footerInline() ? footerH : 0);
    return `${bottom}px`;
  }

  protected cutTop(): boolean {
    return this.isLaser() && this.previewPage() > 1;
  }

  protected cutBottom(): boolean {
    return this.isLaser() && this.previewPage() < this.previewPageCount();
  }

  protected viewportHeight(): string {
    if (this.isLaser()) {
      return `${this.pageHeightPx()}px`;
    }
    const flowH = this.flowMeasure()?.nativeElement.scrollHeight ?? 240;
    return `${this.padTop() + this.padBottom() + Math.max(240, flowH)}px`;
  }

  // ---- Template Management & CRUD ------------------------------------------

  protected async save(): Promise<void> {
    const detail = this.detail();
    if (!detail) return;

    this.bankContentFromDom();
    const draft = this.draftContent();
    if (!draft) return;

    await this.run(async () => {
      const saved = await this.api.update(detail.printTemplateId, {
        templateName: this.draftName().trim(),
        settings: this.draftSettings(),
        content: draft,
        templateVersion: detail.templateVersion,
      });

      this.apply(saved);
      await this.reloadList(saved.printTemplateId);
      this.showFlash('Saved');
    });
  }

  protected async discard(): Promise<void> {
    const detail = this.detail();
    if (!detail) return;

    const ok = await this.confirmDialog.confirm({
      title: 'Discard changes?',
      message: 'Discard changes and restore the saved layout?',
      confirmLabel: 'Discard',
      tone: 'danger',
    });
    if (!ok) return;

    this.apply(detail);
    this.showFlash('Changes discarded');
  }

  protected async newTemplate(): Promise<void> {
    const code = this.documentType();
    if (!code) return;
    const count = this.templates().length;
    const name = `${this.documentTypeName()} — Layout ${count + 1}`;

    await this.run(async () => {
      const created = await this.api.create(code, name);
      this.apply(created);
      await this.reloadList(created.printTemplateId);
    });
  }

  protected async duplicateTemplate(): Promise<void> {
    const detail = this.detail();
    const code = this.documentType();
    if (!detail || !code) return;

    this.bankContentFromDom();
    const name = `${detail.templateName} (copy)`;

    await this.run(async () => {
      const created = await this.api.create(
        code,
        name,
        detail.printTemplateId,
        this.draftSettings(),
        this.draftContent() ?? undefined,
      );
      this.apply(created);
      await this.reloadList(created.printTemplateId);
    });
  }

  protected async deleteTemplate(): Promise<void> {
    const detail = this.detail();
    if (!detail || this.templates().length <= 1) return;

    const ok = await this.confirmDialog.confirm({
      title: 'Delete template?',
      message: `Delete “${detail.templateName}”? Documents using it will fall back to the default.`,
      confirmLabel: 'Delete',
      tone: 'danger',
    });
    if (!ok) return;

    await this.run(async () => {
      await this.api.delete(detail.printTemplateId);
      const remaining = this.templates().filter((t) => t.printTemplateId !== detail.printTemplateId);
      this.templates.set(remaining);
      const next = remaining.find((t) => t.isDefault) ?? remaining[0];
      if (next) {
        await this.select(next.printTemplateId);
      }
    });
  }

  protected async makeDefault(): Promise<void> {
    const detail = this.detail();
    if (!detail) return;

    await this.run(async () => {
      await this.api.setDefault(detail.printTemplateId);
      this.apply(await this.api.get(detail.printTemplateId));
      await this.reloadList(detail.printTemplateId);
      this.showFlash('Set as default');
    });
  }

  protected async resetLayout(): Promise<void> {
    const detail = this.detail();
    if (!detail) return;

    const ok = await this.confirmDialog.confirm({
      title: 'Reset layout?',
      message: 'Put the standard layout back? Your changes to the bands are replaced; the name and page settings are kept.',
      confirmLabel: 'Reset',
      tone: 'danger',
    });
    if (!ok) return;

    await this.run(async () => {
      this.apply(await this.api.reset(detail.printTemplateId));
      this.showFlash('Standard layout restored');
    });
  }

  private async reloadList(selectedId: number): Promise<void> {
    const code = this.documentType();
    if (code !== null) {
      this.templates.set(await this.api.list(code));
    }
    if (this.detail()?.printTemplateId !== selectedId) {
      this.apply(await this.api.get(selectedId));
    }
  }

  private showFlash(text: string): void {
    if (this.flashTimer) clearTimeout(this.flashTimer);
    this.flash.set(text);
    this.flashTimer = setTimeout(() => this.flash.set(null), 2000);
  }

  private async run(action: () => Promise<void>): Promise<void> {
    this.busy.set(true);
    try {
      await action();
    } catch (error) {
      this.fail(error);
    } finally {
      this.busy.set(false);
    }
  }

  private fail(error: unknown): void {
    const failure = readApiFailure(error);
    this.messages.set([{ tone: 'error', text: failure.text, detail: failure.detail }]);
  }

  private async customPlaceholders(code: string): Promise<PlaceholderGroup[]> {
    const kind = CUSTOM_FIELD_KINDS[code];
    if (!kind) {
      return [];
    }
    try {
      const fields = await firstValueFrom(
        this.http.get<CustomFieldDefinition[]>(`/api/custom-fields?kind=${kind}`),
      );
      const printed = fields.filter((f) => f.isActive && f.showOnPrint);
      return printed.length === 0
        ? []
        : [
            {
              group: 'Custom',
              isList: false,
              placeholders: printed.map((f) => ({
                tag: `custom.${f.key}`,
                group: 'Custom',
                description: f.label,
              })),
            },
          ];
    } catch {
      return [];
    }
  }
}
