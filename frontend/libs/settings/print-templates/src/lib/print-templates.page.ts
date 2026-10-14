import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  HostListener,
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
  printHtml,
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

export interface TemplateSnapshot {
  content: PrintContent;
  activeSeg: SegCode;
}

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

const MOCK_ROWS: Record<string, number> = { Item: 4, Tax: 3, Payment: 2, Alloc: 3, Line: 4 };

const MOCK_SINGLE: Record<string, string> = {
  // Organization
  'Organization.Logo': '<img src="assets/bill-book-logo.svg" alt="MERIDIAN" style="max-height:48px;max-width:180px;object-fit:contain;" />',
  'Organization.Name': 'Meridian Trading Co. Pvt Ltd',
  'Organization.OrgName': 'Meridian Trading Co. Pvt Ltd',
  'Organization.CompanyName': 'Meridian Trading Co. Pvt Ltd',
  'Organization.LegalName': 'Meridian Trading Company Private Limited',
  'Organization.TradeName': 'Meridian Trading Co.',
  'Organization.Address': '14 Fort Road, Ballard Estate, Fort<br>Mumbai, Maharashtra 400001',
  'Organization.OrgAddress': '14 Fort Road, Ballard Estate, Fort<br>Mumbai, Maharashtra 400001',
  'Organization.BranchAddress': '14 Fort Road, Ballard Estate, Fort<br>Mumbai, Maharashtra 400001',
  'Organization.Gstin': '27AABCM1234K1Z9',
  'Organization.GSTIN': '27AABCM1234K1Z9',
  'Organization.StateName': 'Maharashtra',
  'Organization.State': 'Maharashtra',
  'Organization.Phone': '+91 22 4021 8890',
  'Organization.OrgPhone': '+91 22 4021 8890',
  'Organization.Email': 'billing@meridiantrading.example.com',
  'Organization.OrgEmail': 'billing@meridiantrading.example.com',
  'Organization.BankName': 'HDFC Bank Ltd',
  'Organization.BankAccountNo': '50200012345678',
  'Organization.BankAccNo': '50200012345678',
  'Organization.AccountNo': '50200012345678',
  'Organization.BankIfsc': 'HDFC0000042',
  'Organization.Ifsc': 'HDFC0000042',
  'Organization.IFSC': 'HDFC0000042',
  'Organization.BankBranch': 'Fort Branch, Mumbai',
  'Organization.Branch': 'Fort Branch, Mumbai',
  'Organization.UpiId': 'meridian@hdfcbank',
  'Organization.UPI': 'meridian@hdfcbank',

  // Document
  'Document.No': 'SI/2026/00418',
  'Document.DocNo': 'SI/2026/00418',
  'Document.Date': '06-Sep-2026',
  'Document.DocDate': '06-Sep-2026',
  'Document.DueDate': '21-Sep-2026',
  'Document.DeliveryDate': '15-Sep-2026',
  'Document.Reference': 'PO/2026/00219',
  'Document.RefNo': 'PO/2026/00219',
  'Document.OriginalDocumentNo': 'SI/2026/00311',
  'Document.OrigDocNo': 'SI/2026/00311',
  'Document.Reason': 'Short supply against original invoice',
  'Document.Status': 'Posted',
  'Document.PlaceOfSupply': '29 - Karnataka',
  'Document.Currency': 'INR',
  'Document.ExchangeRate': '1.00',
  'Document.Notes': 'Thank you for your business. Please remit payment via bank transfer or scan UPI QR code.',
  'Document.Terms': '1. Payment due within 15 days.<br>2. Interest @ 18% p.a. charged on overdue invoices.<br>3. Subject to Mumbai jurisdiction.',
  'Document.VehicleNo': 'MH 12 QT 4417',
  'Document.VehicleNumber': 'MH 12 QT 4417',
  'Document.TransporterName': 'VRL Logistics Ltd',
  'Document.Transporter': 'VRL Logistics Ltd',
  'Document.EwayBillNo': '241088491028',
  'Document.EwbNo': '241088491028',
  'Document.EwayBillDate': '06-Sep-2026',
  'Document.EwbDate': '06-Sep-2026',
  'Document.PaymentMode': 'Bank transfer (NEFT)',
  'Document.BankAccountName': 'HDFC Current A/c - 5678',
  'Document.BarcodeImage': '<div style="display:inline-block;font-family:monospace;padding:2px 8px;border:1px dashed #666;font-size:11px;letter-spacing:3px">||| | | |||| | ||| | |||</div>',
  'Document.Barcode': '<div style="display:inline-block;font-family:monospace;padding:2px 8px;border:1px dashed #666;font-size:11px;letter-spacing:3px">||| | | |||| | ||| | |||</div>',
  'Document.QrImage': '<div style="display:inline-block;width:56px;height:56px;border:2px solid #201f1d;background:repeating-linear-gradient(45deg,#201f1d,#201f1d 3px,#fff 3px,#fff 6px);border-radius:2px" title="QR Code"></div>',
  'Document.QrCode': '<div style="display:inline-block;width:56px;height:56px;border:2px solid #201f1d;background:repeating-linear-gradient(45deg,#201f1d,#201f1d 3px,#fff 3px,#fff 6px);border-radius:2px" title="QR Code"></div>',

  // Party
  'Party.Name': 'Halcyon Interiors Pvt Ltd',
  'Party.PartyName': 'Halcyon Interiors Pvt Ltd',
  'Party.CustomerName': 'Halcyon Interiors Pvt Ltd',
  'Party.VendorName': 'Sundar Steel Works Pvt Ltd',
  'Party.Address': '3rd Floor, Prestige Atrium, Residency Road<br>Bengaluru, Karnataka 560025',
  'Party.BillingAddress': '3rd Floor, Prestige Atrium, Residency Road<br>Bengaluru, Karnataka 560025',
  'Party.ShippingAddress': 'Site 42, Outer Ring Road, Marathahalli<br>Bengaluru, Karnataka 560037',
  'Party.DeliveryAddress': 'Site 42, Outer Ring Road, Marathahalli<br>Bengaluru, Karnataka 560037',
  'Party.Gstin': '29AACCH8821P1ZB',
  'Party.GSTIN': '29AACCH8821P1ZB',
  'Party.GstNo': '29AACCH8821P1ZB',
  'Party.StateName': 'Karnataka',
  'Party.State': 'Karnataka',
  'Party.Phone': '+91 80 4123 9900',
  'Party.Mobile': '+91 80 4123 9900',
  'Party.Email': 'accounts@halcyoninteriors.com',

  // Totals
  'Totals.SubTotal': '62,316.00',
  'Totals.Subtotal': '62,316.00',
  'Totals.GrossAmount': '62,316.00',
  'Totals.GrossTotal': '62,316.00',
  'Totals.Discount': '500.00',
  'Totals.TotalDiscount': '500.00',
  'Totals.TaxableValue': '61,816.00',
  'Totals.TaxableAmount': '61,816.00',
  'Totals.Tax': '11,126.88',
  'Totals.TotalTax': '11,126.88',
  'Totals.TaxAmount': '11,126.88',
  'Totals.RoundOff': '0.12',
  'Totals.Rounding': '0.12',
  'Totals.GrandTotal': '72,943.00',
  'Totals.Total': '72,943.00',
  'Totals.TotalAmount': '72,943.00',
  'Totals.NetAmount': '72,943.00',
  'Totals.InvoiceTotal': '72,943.00',
  'Totals.AmountInWords': 'Rupees Seventy Two Thousand Nine Hundred Forty Three only',
  'Totals.TotalInWords': 'Rupees Seventy Two Thousand Nine Hundred Forty Three only',
  'Totals.Paid': '50,000.00',
  'Totals.PaidAmount': '50,000.00',
  'Totals.Outstanding': '22,943.00',
  'Totals.Balance': '22,943.00',
  'Totals.BalanceDue': '22,943.00',
  'Totals.DueAmount': '22,943.00',

  // Payment UPI
  'Payment.UpiQr': '<div style="display:inline-block;width:72px;height:72px;border:2px solid #16a34a;background:repeating-linear-gradient(45deg,#16a34a,#16a34a 3px,#fff 3px,#fff 6px);border-radius:4px;position:relative" title="UPI QR Code"><span style="position:absolute;bottom:2px;left:2px;right:2px;background:#fff;font-size:8px;font-weight:bold;text-align:center;color:#16a34a">UPI PAY</span></div>',

  // Kitchen
  'Kitchen.Station': 'Main Kitchen',
  'Kitchen.Table': 'Table 14',
  'Kitchen.OrderNo': 'ORD-2041',
  'Kitchen.OrderType': 'Dine-in',
  'Kitchen.Waiter': 'Arun Kumar',
  'Kitchen.Guests': '4',
  'Kitchen.Time': '08:42 PM',
  'Kitchen.Copy': 'ORIGINAL',

  // E-Invoice
  'EInvoice.Irn': '4a28f7d983e201b17d84a7e8f50c0012e8b23a9b1c7849e7a83d2948e7182903',
  'EInvoice.AckNo': '1126100482910',
  'EInvoice.AckDate': '06-Sep-2026',
  'EInvoice.QrImage': '<div style="display:inline-block;width:72px;height:72px;border:2px solid #2563eb;background:repeating-linear-gradient(45deg,#2563eb,#2563eb 3px,#fff 3px,#fff 6px);border-radius:4px;position:relative" title="IRP Signed QR Code"><span style="position:absolute;bottom:2px;left:2px;right:2px;background:#fff;font-size:8px;font-weight:bold;text-align:center;color:#2563eb">IRP QR</span></div>',
  'EInvoice.EwbNo': '241088491028',
  'EInvoice.EwbDate': '06-Sep-2026',
  'EInvoice.EwbValidUntil': '09-Sep-2026',

  // Legacy mappings
  InvoiceNo: 'SI/2026/00418',
  InvoiceDate: '06-Sep-2026',
  DueDate: '21-Sep-2026',
  'Customer.Name': 'Halcyon Interiors Pvt Ltd',
  'Customer.Address': '3rd Floor, Prestige Atrium, Residency Road, Bengaluru 560025',
  'Customer.GSTIN': '29AACCH8821P1ZB',
  PaymentTerms: 'Net 15 days from invoice date',
  PONumber: 'PO/2026/00219',
  PODate: '02-Sep-2026',
  'Vendor.Name': 'Sundar Steel Works Pvt Ltd',
  'Vendor.Address': 'Plot 42, MIDC Phase II, Pune 411026',
  'Vendor.GSTIN': '27AAFCS4471M1Z2',
  DeliveryDate: '15-Sep-2026',
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
  Narration: 'Being depreciation, rent and charges for September 2026',
  PaymentMode: 'Bank transfer',
  PaymentReference: 'UTR 402188173',
  AgainstDocNo: 'SI/2026/00311',
  Subtotal: '62,316.00',
  TotalTax: '11,126.88',
  RoundOff: '0.12',
  TotalAmount: '72,943.00',
  BalanceDue: '22,943.00',
  AmountInWords: 'Rupees Seventy Two Thousand Nine Hundred Forty Three only',
};

const MOCK_ITEMS: [string, string, string, string, string, string, string, string, string, string][] = [
  ['Mild steel angle 50×50×6 mm', 'Grade IS 2062 E250A — standard lengths 6m', '7216', '120.000', 'kg', '78.50', '0.00', '9,420.00', '18.00', '11,115.60'],
  ['MS flat bar 40×6 mm', 'Hot rolled mild steel flats commercial grade', '7214', '86.000', 'kg', '74.20', '0.00', '6,381.20', '18.00', '7,529.82'],
  ['Galvanised sheet 1.6 mm', 'Class 8 zero spangle zinc coated 120 gsm', '7210', '24.000', 'sheet', '1,180.00', '500.00', '27,820.00', '18.00', '32,827.60'],
  ['SS 304 pipe 25 mm', 'Schedule 10 thickness annealed & pickled', '7306', '35.537', 'm', '512.00', '0.00', '18,194.80', '18.00', '21,469.86'],
  ['Powder coating — matte black', 'Pure polyester electrostatic coating', '9988', '160.000', 'sqft', '46.00', '0.00', '7,360.00', '18.00', '8,684.80'],
  ['Hex bolt M10×40 zinc', 'High tensile grade 8.8 zinc plated', '7318', '400.000', 'nos', '11.40', '0.00', '4,560.00', '18.00', '5,380.80'],
  ['Welding consumables', 'AWS E7018 low hydrogen electrodes 3.15 mm', '8311', '18.000', 'kg', '268.00', '0.00', '4,824.00', '18.00', '5,692.32'],
  ['Fabrication labour', 'Structural cutting, fitting & CO2 welding', '9987', '64.000', 'hr', '340.00', '0.00', '21,760.00', '18.00', '25,676.80'],
  ['Site installation charges', 'On-site erection, alignment & commissioning', '9954', '1.000', 'lot', '18,500.00', '0.00', '18,500.00', '18.00', '21,830.00'],
  ['Transport — Mumbai to Bengaluru', 'Dedicated 14ft container vehicle freight', '9965', '1.000', 'trip', '14,200.00', '0.00', '14,200.00', '18.00', '16,756.00'],
];

const MOCK_KOT_ITEMS: [string, string, string, string, string, string, string][] = [
  ['Paneer Butter Masala', 'Medium spicy, extra butter', '9963', '2', 'portion', '280.00', '560.00'],
  ['Butter Naan', 'Crisp, well done', '9963', '4', 'pcs', '45.00', '180.00'],
  ['Jeera Rice', 'Regular portion', '9963', '1', 'bowl', '160.00', '160.00'],
  ['Dal Tadka', 'Garlic tadka, mild', '9963', '1', 'bowl', '210.00', '210.00'],
  ['Gulab Jamun', 'Warm with syrup', '9963', '2', 'portion', '90.00', '180.00'],
];

const MOCK_LIST: Record<string, ((i: number, docType?: string) => string) | string[]> = {
  'Item.SlNo': (i: number) => String(i + 1),
  'Item.SNo': (i: number) => String(i + 1),
  'Item.Sno': (i: number) => String(i + 1),
  'Item.SNO': (i: number) => String(i + 1),
  'Item.Sl.No': (i: number) => String(i + 1),
  'Item.SrNo': (i: number) => String(i + 1),
  'Item.Sr.No': (i: number) => String(i + 1),
  'Item.SerialNo': (i: number) => String(i + 1),
  'Item.No': (i: number) => String(i + 1),
  'Item.ItemName': (i: number, dt?: string) => (dt === 'KOT' ? MOCK_KOT_ITEMS[i % MOCK_KOT_ITEMS.length][0] : MOCK_ITEMS[i % MOCK_ITEMS.length][0]),
  'Item.Name': (i: number, dt?: string) => (dt === 'KOT' ? MOCK_KOT_ITEMS[i % MOCK_KOT_ITEMS.length][0] : MOCK_ITEMS[i % MOCK_ITEMS.length][0]),
  'Item.Description': (i: number, dt?: string) => (dt === 'KOT' ? MOCK_KOT_ITEMS[i % MOCK_KOT_ITEMS.length][1] : MOCK_ITEMS[i % MOCK_ITEMS.length][1]),
  'Item.Desc': (i: number, dt?: string) => (dt === 'KOT' ? MOCK_KOT_ITEMS[i % MOCK_KOT_ITEMS.length][1] : MOCK_ITEMS[i % MOCK_ITEMS.length][1]),
  'Item.HsnSac': (i: number, dt?: string) => (dt === 'KOT' ? MOCK_KOT_ITEMS[i % MOCK_KOT_ITEMS.length][2] : MOCK_ITEMS[i % MOCK_ITEMS.length][2]),
  'Item.HSN': (i: number, dt?: string) => (dt === 'KOT' ? MOCK_KOT_ITEMS[i % MOCK_KOT_ITEMS.length][2] : MOCK_ITEMS[i % MOCK_ITEMS.length][2]),
  'Item.Hsn': (i: number, dt?: string) => (dt === 'KOT' ? MOCK_KOT_ITEMS[i % MOCK_KOT_ITEMS.length][2] : MOCK_ITEMS[i % MOCK_ITEMS.length][2]),
  'Item.Sac': (i: number, dt?: string) => (dt === 'KOT' ? MOCK_KOT_ITEMS[i % MOCK_KOT_ITEMS.length][2] : MOCK_ITEMS[i % MOCK_ITEMS.length][2]),
  'Item.HsnCode': (i: number, dt?: string) => (dt === 'KOT' ? MOCK_KOT_ITEMS[i % MOCK_KOT_ITEMS.length][2] : MOCK_ITEMS[i % MOCK_ITEMS.length][2]),
  'Item.Quantity': (i: number, dt?: string) => (dt === 'KOT' ? MOCK_KOT_ITEMS[i % MOCK_KOT_ITEMS.length][3] : MOCK_ITEMS[i % MOCK_ITEMS.length][3]),
  'Item.Qty': (i: number, dt?: string) => (dt === 'KOT' ? MOCK_KOT_ITEMS[i % MOCK_KOT_ITEMS.length][3] : MOCK_ITEMS[i % MOCK_ITEMS.length][3]),
  'Item.Uom': (i: number, dt?: string) => (dt === 'KOT' ? MOCK_KOT_ITEMS[i % MOCK_KOT_ITEMS.length][4] : MOCK_ITEMS[i % MOCK_ITEMS.length][4]),
  'Item.Unit': (i: number, dt?: string) => (dt === 'KOT' ? MOCK_KOT_ITEMS[i % MOCK_KOT_ITEMS.length][4] : MOCK_ITEMS[i % MOCK_ITEMS.length][4]),
  'Item.UOM': (i: number, dt?: string) => (dt === 'KOT' ? MOCK_KOT_ITEMS[i % MOCK_KOT_ITEMS.length][4] : MOCK_ITEMS[i % MOCK_ITEMS.length][4]),
  'Item.Rate': (i: number, dt?: string) => (dt === 'KOT' ? MOCK_KOT_ITEMS[i % MOCK_KOT_ITEMS.length][5] : MOCK_ITEMS[i % MOCK_ITEMS.length][5]),
  'Item.Price': (i: number, dt?: string) => (dt === 'KOT' ? MOCK_KOT_ITEMS[i % MOCK_KOT_ITEMS.length][5] : MOCK_ITEMS[i % MOCK_ITEMS.length][5]),
  'Item.UnitPrice': (i: number, dt?: string) => (dt === 'KOT' ? MOCK_KOT_ITEMS[i % MOCK_KOT_ITEMS.length][5] : MOCK_ITEMS[i % MOCK_ITEMS.length][5]),
  'Item.Discount': (i: number, dt?: string) => (dt === 'KOT' ? '0.00' : MOCK_ITEMS[i % MOCK_ITEMS.length][6]),
  'Item.Disc': (i: number, dt?: string) => (dt === 'KOT' ? '0.00' : MOCK_ITEMS[i % MOCK_ITEMS.length][6]),
  'Item.TaxableValue': (i: number, dt?: string) => (dt === 'KOT' ? MOCK_KOT_ITEMS[i % MOCK_KOT_ITEMS.length][6] : MOCK_ITEMS[i % MOCK_ITEMS.length][7]),
  'Item.Taxable': (i: number, dt?: string) => (dt === 'KOT' ? MOCK_KOT_ITEMS[i % MOCK_KOT_ITEMS.length][6] : MOCK_ITEMS[i % MOCK_ITEMS.length][7]),
  'Item.TaxRate': (i: number, dt?: string) => (dt === 'KOT' ? '5.00' : MOCK_ITEMS[i % MOCK_ITEMS.length][8]),
  'Item.Tax': (i: number, dt?: string) => (dt === 'KOT' ? '28.00' : (MOCK_ITEMS[i % MOCK_ITEMS.length] ? String((parseFloat(MOCK_ITEMS[i % MOCK_ITEMS.length][9].replace(/,/g, '')) - parseFloat(MOCK_ITEMS[i % MOCK_ITEMS.length][7].replace(/,/g, ''))).toFixed(2)) : '1,695.60')),
  'Item.TaxAmount': (i: number, dt?: string) => (dt === 'KOT' ? '28.00' : (MOCK_ITEMS[i % MOCK_ITEMS.length] ? String((parseFloat(MOCK_ITEMS[i % MOCK_ITEMS.length][9].replace(/,/g, '')) - parseFloat(MOCK_ITEMS[i % MOCK_ITEMS.length][7].replace(/,/g, ''))).toFixed(2)) : '1,695.60')),
  'Item.Amount': (i: number, dt?: string) => (dt === 'KOT' ? MOCK_KOT_ITEMS[i % MOCK_KOT_ITEMS.length][6] : MOCK_ITEMS[i % MOCK_ITEMS.length][9]),
  'Item.Total': (i: number, dt?: string) => (dt === 'KOT' ? MOCK_KOT_ITEMS[i % MOCK_KOT_ITEMS.length][6] : MOCK_ITEMS[i % MOCK_ITEMS.length][9]),
  'Item.LineTotal': (i: number, dt?: string) => (dt === 'KOT' ? MOCK_KOT_ITEMS[i % MOCK_KOT_ITEMS.length][6] : MOCK_ITEMS[i % MOCK_ITEMS.length][9]),
  'Tax.Component': ['CGST', 'SGST', 'IGST'],
  'Tax.Name': ['CGST', 'SGST', 'IGST'],
  'Tax.Type': ['CGST', 'SGST', 'IGST'],
  'Tax.Rate': ['9.00', '9.00', '18.00'],
  'Tax.Percent': ['9.00', '9.00', '18.00'],
  'Tax.Percentage': ['9.00', '9.00', '18.00'],
  'Tax.TaxRate': ['9.00', '9.00', '18.00'],
  'Tax.TaxableValue': ['61,816.00', '61,816.00', '61,816.00'],
  'Tax.Taxable': ['61,816.00', '61,816.00', '61,816.00'],
  'Tax.TaxableAmount': ['61,816.00', '61,816.00', '61,816.00'],
  'Tax.Amount': ['5,563.44', '5,563.44', '11,126.88'],
  'Tax.Tax': ['5,563.44', '5,563.44', '11,126.88'],
  'Tax.TaxAmount': ['5,563.44', '5,563.44', '11,126.88'],
  'Payment.Date': ['28-Aug-2026', '04-Sep-2026'],
  'Payment.Mode': ['Bank transfer (NEFT)', 'UPI transfer'],
  'Payment.PaymentMode': ['Bank transfer (NEFT)', 'UPI transfer'],
  'Payment.Reference': ['UTR 402188173', 'UPI 6620041188'],
  'Payment.Ref': ['UTR 402188173', 'UPI 6620041188'],
  'Payment.RefNo': ['UTR 402188173', 'UPI 6620041188'],
  'Payment.Amount': ['35,000.00', '15,000.00'],
  'Payment.PaidAmount': ['35,000.00', '15,000.00'],
  'Alloc.DocumentNo': ['SI/2026/00311', 'SI/2026/00344', 'SI/2026/00418'],
  'Alloc.DocNo': ['SI/2026/00311', 'SI/2026/00344', 'SI/2026/00418'],
  'Alloc.DocumentDate': ['12-Aug-2026', '24-Aug-2026', '06-Sep-2026'],
  'Alloc.Date': ['12-Aug-2026', '24-Aug-2026', '06-Sep-2026'],
  'Alloc.DocumentTotal': ['84,200.00', '1,12,400.00', '72,943.00'],
  'Alloc.Total': ['84,200.00', '1,12,400.00', '72,943.00'],
  'Alloc.Amount': ['50,000.00', '22,943.00', '72,943.00'],
  'Alloc.AllocatedAmount': ['50,000.00', '22,943.00', '72,943.00'],
  'Alloc.Balance': ['34,200.00', '89,457.00', '0.00'],
  'Alloc.BalanceDue': ['34,200.00', '89,457.00', '0.00'],
  'Line.AccountCode': ['4010', '1510', '5020', '2010'],
  'Line.AccountName': [
    'Depreciation Expense',
    'Accumulated Depreciation',
    'Office Rent Expense',
    'Rent Payable',
  ],
  'Line.Account': [
    'Depreciation Expense',
    'Accumulated Depreciation',
    'Office Rent Expense',
    'Rent Payable',
  ],
  'Line.Description': [
    'Plant & machinery depreciation for Sep 2026',
    'Depreciation reserve provision',
    'September 2026 premises rent',
    'Payable to landlord',
  ],
  'Line.Narration': [
    'Plant & machinery depreciation for Sep 2026',
    'Depreciation reserve provision',
    'September 2026 premises rent',
    'Payable to landlord',
  ],
  'Line.Debit': ['18,400.00', '', '65,000.00', ''],
  'Line.Credit': ['', '18,400.00', '', '65,000.00'],
  'Line.Amount': ['18,400.00', '18,400.00', '65,000.00', '65,000.00'],
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
export class PrintTemplatesPage implements OnInit {
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
  protected readonly currentFontSize = signal<number>(11);
  protected readonly selectedCellInfo = signal<{
    hasCell: boolean;
    colWidthPct: number;
    rowHeightPx: number;
    colIndex: number;
    rowIndex: number;
  }>({
    hasCell: false,
    colWidthPct: 0,
    rowHeightPx: 0,
    colIndex: 0,
    rowIndex: 0,
  });
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

  // History Stack (Ctrl+Z / Ctrl+Y)
  protected readonly undoStack = signal<TemplateSnapshot[]>([]);
  protected readonly redoStack = signal<TemplateSnapshot[]>([]);
  protected readonly canUndo = computed(() => this.undoStack().length > 1);
  protected readonly canRedo = computed(() => this.redoStack().length > 0);

  // Section Grid & Split Guidelines
  protected readonly showSplits = signal<boolean>(true);
  protected readonly customRows = signal<number>(1);
  protected readonly customCols = signal<number>(2);

  // Column Width Mouse Drag & Drop State
  protected readonly colResizeState = signal<{
    segCode: SegCode;
    table: HTMLTableElement;
    colIndex: number;
    startX: number;
    currentX: number;
    startW1: number;
    startW2: number;
    totalW: number;
    startPct1: number;
    startPct2: number;
    curPct1: number;
    curPct2: number;
    top: number;
    bottom: number;
  } | null>(null);

  // Page-level typography
  protected readonly pageFontFamily = signal<string>('Arial');
  protected readonly pageFontSize = signal<number>(11);

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
      const [fetchedTemplates, placeholders] = await Promise.all([
        this.api.list(code),
        this.api.placeholders(code),
      ]);

      if (this.documentType() !== code) {
        return;
      }

      let templates = fetchedTemplates;
      if (templates.length === 0 && this.canEdit()) {
        try {
          const typeName = this.documentTypeName() || code;
          const created = await this.api.create(code, typeName);
          templates = [created];
        } catch {
          // If creation fails, proceed with empty list
        }
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

    this.undoStack.set([{ content: { ...detail.content }, activeSeg: 'd' }]);
    this.redoStack.set([]);

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
        if (this.pageFontFamily()) {
          ref.nativeElement.style.fontFamily = this.pageFontFamily();
        }
        if (this.pageFontSize()) {
          ref.nativeElement.style.fontSize = `${this.pageFontSize()}px`;
        }
      }
    });
  }

  // ---- Tag <-> Chip conversions ---------------------------------------------

  protected tagToChip(html: string): string {
    if (!html) return '';
    // Heal any previously corrupted empty chips or encoded tags
    const clean = html.replace(
      /<span[^>]*class="[^"]*pt-chip[^"]*"[^>]*>([\s\S]*?)<\/span>/gi,
      (_match, inner) => {
        const text = inner
          .replace(/<[^>]+>/g, '')
          .replace(/&lt;/g, '<')
          .replace(/&gt;/g, '>')
          .replace(/[↻\s«»{}<>]/g, '')
          .trim();
        if (text) {
          return `<<${text}>>`;
        }
        return '';
      },
    );

    return clean.replace(
      /(?:\{\{([^{}]+)\}\}|(?:<<|«|&lt;&lt;)([^<>«»&]+)(?:>>|»|&gt;&gt;))/g,
      (_match, tag1, tag2) => {
        const rawTag = (tag1 ?? tag2 ?? '')
          .replace(/&lt;/g, '<')
          .replace(/&gt;/g, '>')
          .replace(/[↻\s«»{}<>]/g, '')
          .trim();
        if (!rawTag) return '';
        const isList = tag2 != null || this.isListTag(rawTag);
        return `<span contenteditable="false" class="pt-chip${isList ? ' pt-chip--list' : ''}">${isList ? `&lt;&lt;${rawTag}&gt;&gt;` : `{{${rawTag}}}`}</span>\u00a0`;
      },
    );
  }

  protected chipToTag(html: string): string {
    if (!html) return '';
    const div = document.createElement('div');
    div.innerHTML = html;
    const chips = div.querySelectorAll('.pt-chip');
    chips.forEach((chip) => {
      const raw = chip.textContent || '';
      const cleanTag = raw
        .replace(/&lt;/g, '<')
        .replace(/&gt;/g, '>')
        .replace(/[↻\s«»{}<>]/g, '')
        .trim();
      if (!cleanTag) {
        chip.remove();
        return;
      }
      const isList =
        chip.classList.contains('pt-chip--list') ||
        raw.includes('<<') ||
        raw.includes('«') ||
        this.isListTag(cleanTag);
      const tagNode = document.createTextNode(isList ? `<<${cleanTag}>>` : `{{${cleanTag}}}`);
      chip.replaceWith(tagNode);
    });
    return div.innerHTML;
  }

  protected isListTag(tag: string): boolean {
    const clean = tag
      .replace(/&lt;/g, '<')
      .replace(/&gt;/g, '>')
      .replace(/[↻\s«»{}<>]/g, '')
      .trim();
    const dot = clean.indexOf('.');
    if (dot > 0) {
      const prefix = clean.substring(0, dot).toLowerCase();
      if (['item', 'tax', 'payment', 'alloc', 'line'].includes(prefix)) {
        return true;
      }
    }
    for (const group of this.placeholders()) {
      if (group.isList && group.placeholders.some((p) => p.tag.toLowerCase() === clean.toLowerCase())) {
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
    if (this.activeSeg() === code) {
      return;
    }
    this.activeSeg.set(code);
    setTimeout(() => {
      const el = this.segElements().find(
        (ref) => ref.nativeElement.dataset['seg'] === code,
      )?.nativeElement;
      if (el && !el.contains(document.activeElement)) {
        el.focus();
        this.onSnap();
      }
    });
  }

  private setActiveCell(cell: HTMLElement | null): void {
    if (this.activeCell && this.activeCell !== cell) {
      this.activeCell.classList.remove('pt-cell-active');
    }
    this.activeCell = cell;
    if (this.activeCell) {
      this.activeCell.classList.add('pt-cell-active');
    }
    this.updateSelectedCellInfo();
  }

  private getTableContext(): { table: HTMLTableElement; row: HTMLTableRowElement; cell: HTMLTableCellElement } | null {
    let cell = this.activeCell as HTMLTableCellElement | null;
    if (!cell) {
      const sel = window.getSelection();
      let n = sel && sel.anchorNode;
      while (n && n.nodeType !== 1) n = n.parentNode;
      cell = (n as HTMLElement)?.closest ? ((n as HTMLElement).closest('td,th') as HTMLTableCellElement | null) : null;
    }
    if (!cell) return null;
    const row = cell.closest('tr') as HTMLTableRowElement | null;
    const table = cell.closest('table') as HTMLTableElement | null;
    if (!row || !table) return null;
    return { table, row, cell };
  }

  private getColIndex(row: HTMLTableRowElement, cell: HTMLTableCellElement): number {
    let idx = 0;
    for (const c of Array.from(row.children) as HTMLTableCellElement[]) {
      if (c === cell) return idx;
      idx += (c.colSpan || 1);
    }
    return idx;
  }

  private updateSelectedCellInfo(): void {
    const ctx = this.getTableContext();
    if (!ctx) {
      this.selectedCellInfo.set({
        hasCell: false,
        colWidthPct: 0,
        rowHeightPx: 0,
        colIndex: 0,
        rowIndex: 0,
      });
      return;
    }
    const { table, row, cell } = ctx;
    const colIdx = this.getColIndex(row, cell) + 1;
    const rowIdx = Array.from(table.rows).indexOf(row) + 1;

    let widthPct = 0;
    if (cell.style.width && cell.style.width.endsWith('%')) {
      widthPct = parseFloat(cell.style.width) || 0;
    } else if (cell.getAttribute('width') && cell.getAttribute('width')!.endsWith('%')) {
      widthPct = parseFloat(cell.getAttribute('width')!) || 0;
    } else if (table.offsetWidth > 0 && cell.offsetWidth > 0) {
      widthPct = Math.round((cell.offsetWidth / table.offsetWidth) * 100);
    }

    let heightPx = 0;
    if (row.style.height && row.style.height.endsWith('px')) {
      heightPx = parseFloat(row.style.height) || 0;
    } else if (row.offsetHeight > 0) {
      heightPx = Math.round(row.offsetHeight);
    }

    this.selectedCellInfo.set({
      hasCell: true,
      colWidthPct: widthPct,
      rowHeightPx: heightPx,
      colIndex: colIdx,
      rowIndex: rowIdx,
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

  protected onSnap(e?: Event): void {
    const sel = window.getSelection();
    let targetNode: Node | null = null;
    if (sel && sel.rangeCount > 0) {
      this.activeRange = sel.getRangeAt(0).cloneRange();
      targetNode = sel.anchorNode;
    }
    if (!targetNode && e && e.target) {
      targetNode = e.target as Node;
    }
    let node: Node | null = targetNode;
    while (node && node.nodeType !== 1) {
      node = node.parentNode;
    }
    const el = node as HTMLElement | null;
    let cell: HTMLElement | null = el?.closest ? el.closest('td,th') : null;
    if (!cell && e && (e.target as HTMLElement)?.closest) {
      cell = (e.target as HTMLElement).closest('td,th');
    }
    this.setActiveCell(cell);
    this.syncFmt(el);
  }

  private syncFmt(clickedEl: HTMLElement | null = null): void {
    const q = (c: string) => {
      try {
        return document.queryCommandState(c);
      } catch {
        return false;
      }
    };

    let el: HTMLElement | null = clickedEl;
    if (!el) {
      const sel = window.getSelection();
      let n = sel && sel.anchorNode;
      while (n && n.nodeType !== 1) n = n.parentNode;
      el = n as HTMLElement | null;
    }
    if (!el && this.activeCell) {
      el = this.activeCell;
    }

    // Bold detection
    let isBold = q('bold');
    if (!isBold && el) {
      let curr: HTMLElement | null = el;
      while (curr && curr.dataset['seg'] === undefined) {
        if (curr.tagName === 'B' || curr.tagName === 'STRONG') {
          isBold = true;
          break;
        }
        const fw = getComputedStyle(curr).fontWeight;
        if (fw === 'bold' || fw === 'bolder' || parseInt(fw, 10) >= 600) {
          isBold = true;
          break;
        }
        curr = curr.parentElement;
      }
    }

    // Italic detection
    let isItalic = q('italic');
    if (!isItalic && el) {
      let curr: HTMLElement | null = el;
      while (curr && curr.dataset['seg'] === undefined) {
        if (curr.tagName === 'I' || curr.tagName === 'EM') {
          isItalic = true;
          break;
        }
        if (getComputedStyle(curr).fontStyle === 'italic') {
          isItalic = true;
          break;
        }
        curr = curr.parentElement;
      }
    }

    // Underline detection
    let isUnderline = q('underline');
    if (!isUnderline && el) {
      let curr: HTMLElement | null = el;
      while (curr && curr.dataset['seg'] === undefined) {
        if (curr.tagName === 'U') {
          isUnderline = true;
          break;
        }
        const td = getComputedStyle(curr).textDecorationLine || getComputedStyle(curr).textDecoration;
        if (td && td.includes('underline')) {
          isUnderline = true;
          break;
        }
        curr = curr.parentElement;
      }
    }

    // Strikethrough detection
    let isStrike = q('strikeThrough');
    if (!isStrike && el) {
      let curr: HTMLElement | null = el;
      while (curr && curr.dataset['seg'] === undefined) {
        if (curr.tagName === 'S' || curr.tagName === 'STRIKE' || curr.tagName === 'DEL') {
          isStrike = true;
          break;
        }
        const td = getComputedStyle(curr).textDecorationLine || getComputedStyle(curr).textDecoration;
        if (td && td.includes('line-through')) {
          isStrike = true;
          break;
        }
        curr = curr.parentElement;
      }
    }

    // Paragraph Alignment detection
    let isLeft = false;
    let isCenter = false;
    let isRight = false;
    let isJustify = false;

    if (el) {
      let curr: HTMLElement | null = el;
      let alignVal = '';
      while (curr && curr.dataset['seg'] === undefined) {
        const styleAlign = curr.style.textAlign;
        const attrAlign = curr.getAttribute('align');
        const compAlign = getComputedStyle(curr).textAlign;
        if (styleAlign) {
          alignVal = styleAlign;
          break;
        }
        if (attrAlign) {
          alignVal = attrAlign;
          break;
        }
        if (compAlign && compAlign !== 'start' && compAlign !== 'left') {
          alignVal = compAlign;
          break;
        }
        if (curr.tagName === 'TD' || curr.tagName === 'TH') {
          if (attrAlign) {
            alignVal = attrAlign;
            break;
          }
        }
        curr = curr.parentElement;
      }
      if (!alignVal && el) {
        alignVal = getComputedStyle(el).textAlign;
      }

      if (alignVal === 'right' || alignVal === 'end' || alignVal === '-webkit-right') {
        isRight = true;
      } else if (alignVal === 'center' || alignVal === '-webkit-center') {
        isCenter = true;
      } else if (alignVal === 'justify') {
        isJustify = true;
      } else {
        isLeft = true;
      }
    } else {
      isLeft = true;
    }

    const f = {
      b: isBold,
      i: isItalic,
      u: isUnderline,
      s: isStrike,
      l: isLeft,
      c: isCenter,
      r: isRight,
      j: isJustify,
      ul: q('insertUnorderedList') || !!el?.closest('ul'),
      ol: q('insertOrderedList') || !!el?.closest('ol'),
    };
    this.ptFmt.set(f);

    // Font Family & Size Detection
    if (el) {
      let curr: HTMLElement | null = el;
      while (curr && curr.dataset['seg'] === undefined) {
        const ff = curr.style.fontFamily || getComputedStyle(curr).fontFamily;
        if (ff) {
          const cleanFont = ff.split(',')[0].replace(/['"]/g, '').trim();
          const match = this.fontFamilies.find((font) => font.toLowerCase() === cleanFont.toLowerCase());
          if (match) {
            this.lastFont.set(match);
            break;
          }
        }
        curr = curr.parentElement;
      }

      curr = el;
      while (curr && curr.dataset['seg'] === undefined) {
        const fs = curr.style.fontSize || getComputedStyle(curr).fontSize;
        if (fs) {
          const px = Math.round(parseFloat(fs));
          if (px > 0 && this.fontSizes.includes(px)) {
            this.currentFontSize.set(px);
            break;
          }
        }
        curr = curr.parentElement;
      }
    }
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
    const seg = this.segElements().find(
      (ref) => ref.nativeElement.dataset['seg'] === this.activeSeg(),
    )?.nativeElement;

    if (this.activeCell) {
      this.activeCell.style.textAlign = value;
      this.activeCell.removeAttribute('align');
      Array.from(this.activeCell.querySelectorAll('div, p, h1, h2, h3')).forEach((el) => {
        (el as HTMLElement).style.textAlign = value;
        el.removeAttribute('align');
      });
    } else if (sel && sel.rangeCount > 0) {
      const range = sel.getRangeAt(0);
      const blockOf = (n: Node | null): HTMLElement | null => {
        while (n && n.nodeType !== 1) n = n.parentNode;
        const el = n && (n as HTMLElement).closest ? (n as HTMLElement).closest('td,th,li,p,h1,h2,h3,div') : null;
        return el && seg && seg.contains(el) && el !== seg ? (el as HTMLElement) : seg || null;
      };

      let targets: HTMLElement[] = [];
      if (!range.collapsed) {
        const scope = blockOf(range.commonAncestorContainer) || seg;
        if (scope) {
          targets = Array.from(scope.querySelectorAll('td,th,li,p,div')).filter((el) =>
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
    }
    this.onSnap();
    this.onSegmentInput(this.activeSeg());
  }

  protected setFontFamily(font: string): void {
    this.lastFont.set(font);
    if (this.activeCell && (!this.activeRange || this.activeRange.collapsed)) {
      this.activeCell.style.fontFamily = font;
    } else {
      this.execSel('fontName', font);
    }
    this.onSegmentInput(this.activeSeg());
  }

  protected setFontSize(px: number): void {
    this.currentFontSize.set(px);
    if (this.activeCell && (!this.activeRange || this.activeRange.collapsed)) {
      this.activeCell.style.fontSize = `${px}px`;
    } else {
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
    }
    this.onSegmentInput(this.activeSeg());
  }

  protected applyPageFont(): void {
    const font = this.lastFont();
    const sz = this.currentFontSize();
    this.recordSnapshot(true);

    this.pageFontFamily.set(font);
    this.pageFontSize.set(sz);

    for (const ref of this.segElements()) {
      const el = ref.nativeElement;
      const code = el.dataset['seg'] as SegCode;
      if (!el || !code) continue;

      el.style.fontFamily = font;
      el.style.fontSize = `${sz}px`;

      const elements = el.querySelectorAll<HTMLElement>('*');
      elements.forEach((node) => {
        node.style.fontFamily = font;

        if (node.tagName.toLowerCase() === 'font') {
          node.removeAttribute('face');
          node.removeAttribute('size');
        }

        if (
          ['TD', 'TH', 'TABLE', 'TR', 'DIV', 'P', 'SPAN', 'B', 'STRONG', 'I', 'EM', 'FONT'].includes(
            node.tagName,
          )
        ) {
          if (node.classList.contains('pt-chip')) {
            node.style.fontFamily = font;
          } else {
            node.style.fontFamily = font;
            node.style.fontSize = `${sz}px`;
          }
        }
      });

      const segDef = SEGMENTS.find((s) => s.code === code);
      if (segDef) {
        const tagHtml = this.getSegmentTagHtml(code);
        const draft = this.draftContent();
        if (draft) {
          this.draftContent.set({ ...draft, [segDef.contentKey]: tagHtml });
        }
      }
    }

    this.showFlash(`Applied ${font} (${sz}px) to all sections`);
    this.recordSnapshot(true);
  }

  // ---- History Stack (Undo / Redo) ------------------------------------------

  private historyDebounceTimer: ReturnType<typeof setTimeout> | null = null;

  protected recordSnapshot(immediate = false): void {
    const snapshot = (): void => {
      const currentContent: PrintContent = {
        fixedHeaderHtml: this.getSegmentTagHtml('fh'),
        headerHtml: this.getSegmentTagHtml('h'),
        detailsHtml: this.getSegmentTagHtml('d'),
        footerHtml: this.getSegmentTagHtml('f'),
        fixedFooterHtml: this.getSegmentTagHtml('ff'),
      };
      const snap: TemplateSnapshot = {
        content: currentContent,
        activeSeg: this.activeSeg(),
      };
      const stack = this.undoStack();
      const last = stack[stack.length - 1];
      if (
        last &&
        JSON.stringify(last.content) === JSON.stringify(snap.content) &&
        last.activeSeg === snap.activeSeg
      ) {
        return;
      }
      this.undoStack.update((s) => [...s.slice(-30), snap]);
      this.redoStack.set([]);
    };

    if (immediate) {
      if (this.historyDebounceTimer) {
        clearTimeout(this.historyDebounceTimer);
        this.historyDebounceTimer = null;
      }
      snapshot();
    } else {
      if (this.historyDebounceTimer) clearTimeout(this.historyDebounceTimer);
      this.historyDebounceTimer = setTimeout(() => {
        snapshot();
      }, 350);
    }
  }

  protected undo(): void {
    const stack = this.undoStack();
    if (stack.length <= 1) return;
    const current = stack[stack.length - 1];
    const prev = stack[stack.length - 2];
    this.undoStack.set(stack.slice(0, -1));
    this.redoStack.update((r) => [...r, current]);
    this.draftContent.set(prev.content);
    this.loadContentIntoDom(prev.content);
    this.activeSeg.set(prev.activeSeg);
    this.showFlash('Undone');
  }

  protected redo(): void {
    const rStack = this.redoStack();
    if (rStack.length === 0) return;
    const next = rStack[rStack.length - 1];
    this.redoStack.set(rStack.slice(0, -1));
    this.undoStack.update((u) => [...u, next]);
    this.draftContent.set(next.content);
    this.loadContentIntoDom(next.content);
    this.activeSeg.set(next.activeSeg);
    this.showFlash('Redone');
  }

  @HostListener('window:keydown', ['$event'])
  protected handleKeyDown(e: KeyboardEvent): void {
    if (this.mode() !== 'edit' || !this.canEdit()) return;
    if (e.ctrlKey || e.metaKey) {
      if (e.key.toLowerCase() === 'z' && !e.shiftKey) {
        e.preventDefault();
        this.undo();
      } else if ((e.key.toLowerCase() === 'z' && e.shiftKey) || e.key.toLowerCase() === 'y') {
        e.preventDefault();
        this.redo();
      }
    }
  }

  // ---- Section Grid & Split Configuration -----------------------------------

  protected toggleSplits(): void {
    this.showSplits.update((v) => !v);
  }

  protected setCustomRows(rows: number): void {
    this.customRows.set(Math.max(1, Math.min(10, rows)));
  }

  protected setCustomCols(cols: number): void {
    this.customCols.set(Math.max(1, Math.min(6, cols)));
  }

  protected applyCustomSplit(): void {
    this.applySectionSplit(this.customRows(), this.customCols());
  }

  protected applySectionSplit(rows: number, cols: number, ratios?: number[]): void {
    if (rows < 1) rows = 1;
    if (cols < 1) cols = 1;
    this.recordSnapshot(true);

    const segEl = this.segElements().find(
      (ref) => ref.nativeElement.dataset['seg'] === this.activeSeg(),
    )?.nativeElement;
    if (!segEl) return;

    const existingContents: string[] = [];
    const existingTds = segEl.querySelectorAll('td, th');
    if (existingTds.length > 0) {
      existingTds.forEach((c) => {
        const h = c.innerHTML.trim();
        if (h && h !== '&nbsp;') existingContents.push(h);
      });
    } else {
      const rawHtml = segEl.innerHTML.trim();
      if (rawHtml && rawHtml !== '<br>' && rawHtml !== '&nbsp;') {
        existingContents.push(rawHtml);
      }
    }

    const colWidths: number[] = [];
    if (ratios && ratios.length === cols) {
      const sum = ratios.reduce((a, b) => a + b, 0);
      for (const r of ratios) {
        colWidths.push(Math.round((r / sum) * 1000) / 10);
      }
    } else {
      const equalWidth = Math.round((100 / cols) * 10) / 10;
      for (let c = 0; c < cols; c++) {
        colWidths.push(equalWidth);
      }
    }

    const font = this.pageFontFamily() || this.lastFont() || 'Arial';
    const sz = this.pageFontSize() || this.currentFontSize() || 11;

    let tableHtml = '<table style="width: 100%; border-collapse: collapse; margin-bottom: 4px;">\n  <tbody>\n';
    let contentIdx = 0;
    for (let r = 0; r < rows; r++) {
      tableHtml += '    <tr>\n';
      for (let c = 0; c < cols; c++) {
        const w = colWidths[c];
        const content = contentIdx < existingContents.length ? existingContents[contentIdx++] : '&nbsp;';
        tableHtml += `      <td style="width: ${w}%; padding: 6px 8px; vertical-align: top; border: 1px dashed rgba(160, 185, 215, 0.45); font-family: ${font}; font-size: ${sz}px;">${content}</td>\n`;
      }
      tableHtml += '    </tr>\n';
    }
    tableHtml += '  </tbody>\n</table>';

    segEl.innerHTML = tableHtml;
    this.onSegmentInput(this.activeSeg());
    this.recordSnapshot(true);
    this.showFlash(`Configured ${this.activeSegLabel()} into ${rows} row${rows > 1 ? 's' : ''} × ${cols} column${cols > 1 ? 's' : ''}`);
  }

  protected addSectionRow(): void {
    const segEl = this.segElements().find(
      (ref) => ref.nativeElement.dataset['seg'] === this.activeSeg(),
    )?.nativeElement;
    if (!segEl) return;
    const table = segEl.querySelector('table');
    if (table && table.rows.length > 0) {
      const lastRow = table.rows[table.rows.length - 1];
      const newRow = this.createMatchingRow(lastRow);
      lastRow.parentNode?.insertBefore(newRow, lastRow.nextSibling);
      this.onSegmentInput(this.activeSeg());
      this.recordSnapshot(true);
      this.showFlash(`Added row to ${this.activeSegLabel()}`);
    } else {
      this.applySectionSplit(2, 2);
    }
  }

  protected removeSectionRow(): void {
    const segEl = this.segElements().find(
      (ref) => ref.nativeElement.dataset['seg'] === this.activeSeg(),
    )?.nativeElement;
    if (!segEl) return;
    const table = segEl.querySelector('table');
    if (table && table.rows.length > 1) {
      table.rows[table.rows.length - 1].remove();
      this.onSegmentInput(this.activeSeg());
      this.recordSnapshot(true);
      this.showFlash(`Removed row from ${this.activeSegLabel()}`);
    }
  }

  protected addSectionCol(): void {
    const segEl = this.segElements().find(
      (ref) => ref.nativeElement.dataset['seg'] === this.activeSeg(),
    )?.nativeElement;
    if (!segEl) return;
    const table = segEl.querySelector('table');
    if (table && table.rows.length > 0) {
      this.recordSnapshot(true);
      for (const row of Array.from(table.rows)) {
        const newCell = document.createElement('td');
        const font = this.pageFontFamily() || this.lastFont() || 'Arial';
        const sz = this.pageFontSize() || this.currentFontSize() || 11;
        newCell.style.cssText = `border: 1px dashed rgba(160, 185, 215, 0.45); padding: 6px 8px; font-family: ${font}; font-size: ${sz}px; vertical-align: top;`;
        newCell.innerHTML = '&nbsp;';
        row.appendChild(newCell);
      }
      for (const row of Array.from(table.rows)) {
        const cells = Array.from(row.children) as HTMLElement[];
        const equalW = Math.round((100 / cells.length) * 10) / 10;
        cells.forEach((c) => (c.style.width = `${equalW}%`));
      }
      this.onSegmentInput(this.activeSeg());
      this.recordSnapshot(true);
      this.showFlash(`Added column to ${this.activeSegLabel()}`);
    } else {
      this.applySectionSplit(1, 2);
    }
  }

  protected removeSectionCol(): void {
    const segEl = this.segElements().find(
      (ref) => ref.nativeElement.dataset['seg'] === this.activeSeg(),
    )?.nativeElement;
    if (!segEl) return;
    const table = segEl.querySelector('table');
    if (table && table.rows.length > 0 && table.rows[0].children.length > 1) {
      this.recordSnapshot(true);
      for (const row of Array.from(table.rows)) {
        if (row.children.length > 1) {
          row.removeChild(row.lastElementChild!);
        }
      }
      for (const row of Array.from(table.rows)) {
        const cells = Array.from(row.children) as HTMLElement[];
        const equalW = Math.round((100 / cells.length) * 10) / 10;
        cells.forEach((c) => (c.style.width = `${equalW}%`));
      }
      this.onSegmentInput(this.activeSeg());
      this.recordSnapshot(true);
      this.showFlash(`Removed column from ${this.activeSegLabel()}`);
    }
  }

  protected pickFore(color: string): void {
    this.foreColor.set(color);
    if (this.activeCell && (!this.activeRange || this.activeRange.collapsed)) {
      this.activeCell.style.color = color;
    } else {
      this.execSel('foreColor', color);
    }
    this.onSegmentInput(this.activeSeg());
  }

  protected pickHilite(color: string): void {
    this.hiliteColor.set(color);
    if (this.activeCell && (!this.activeRange || this.activeRange.collapsed)) {
      this.activeCell.style.backgroundColor = color;
    } else {
      this.execSel('hiliteColor', color);
    }
    this.onSegmentInput(this.activeSeg());
  }

  // ---- Table Manipulation ---------------------------------------------------

  protected insertTable(): void {
    const cell = '<td style="border:1px solid #ccc;padding:4px 6px;font-size:11px">&nbsp;</td>';
    this.execSel(
      'insertHTML',
      `<table style="width:100%;border-collapse:collapse;margin:6px 0"><tr>${cell}${cell}</tr><tr>${cell}${cell}</tr></table>`,
    );
  }

  protected rowAbove(): void {
    const ctx = this.getTableContext();
    if (!ctx) return;
    const { row } = ctx;
    const newRow = this.createMatchingRow(row);
    row.parentNode?.insertBefore(newRow, row);
    this.onSegmentInput(this.activeSeg());
    this.updateSelectedCellInfo();
  }

  protected rowBelow(): void {
    const ctx = this.getTableContext();
    if (!ctx) return;
    const { row } = ctx;
    const newRow = this.createMatchingRow(row);
    row.parentNode?.insertBefore(newRow, row.nextSibling);
    this.onSegmentInput(this.activeSeg());
    this.updateSelectedCellInfo();
  }

  private createMatchingRow(refRow: HTMLTableRowElement): HTMLTableRowElement {
    const newRow = document.createElement('tr');
    Array.from(refRow.children).forEach((c) => {
      const cell = c as HTMLTableCellElement;
      const newCell = document.createElement('td');
      newCell.style.cssText = 'border:1px solid #ccc;padding:4px 6px;font-size:11px';
      if (cell.style.width) newCell.style.width = cell.style.width;
      if (cell.getAttribute('width')) newCell.setAttribute('width', cell.getAttribute('width')!);
      if (cell.colSpan > 1) newCell.colSpan = cell.colSpan;
      newCell.innerHTML = '&nbsp;';
      newRow.appendChild(newCell);
    });
    return newRow;
  }

  protected colLeft(): void {
    const ctx = this.getTableContext();
    if (!ctx) return;
    const { table, row, cell } = ctx;
    const colIdx = this.getColIndex(row, cell);

    for (const r of Array.from(table.rows)) {
      let cIdx = 0;
      let inserted = false;
      for (const c of Array.from(r.children) as HTMLTableCellElement[]) {
        const cSpan = c.colSpan || 1;
        if (cIdx <= colIdx && colIdx < cIdx + cSpan) {
          if (cSpan > 1 && c !== cell) {
            c.colSpan = cSpan + 1;
          } else {
            const newCell = document.createElement(c.tagName === 'TH' ? 'th' : 'td');
            newCell.style.cssText = 'border:1px solid #ccc;padding:4px 6px;font-size:11px';
            newCell.innerHTML = '&nbsp;';
            c.parentNode?.insertBefore(newCell, c);
          }
          inserted = true;
          break;
        }
        cIdx += cSpan;
      }
      if (!inserted) {
        const newCell = document.createElement('td');
        newCell.style.cssText = 'border:1px solid #ccc;padding:4px 6px;font-size:11px';
        newCell.innerHTML = '&nbsp;';
        r.insertBefore(newCell, r.firstChild);
      }
    }
    this.onSegmentInput(this.activeSeg());
    this.updateSelectedCellInfo();
  }

  protected colRight(): void {
    const ctx = this.getTableContext();
    if (!ctx) return;
    const { table, row, cell } = ctx;
    const colIdx = this.getColIndex(row, cell);
    const span = cell.colSpan || 1;
    const targetCol = colIdx + span - 1;

    for (const r of Array.from(table.rows)) {
      let cIdx = 0;
      let inserted = false;
      for (const c of Array.from(r.children) as HTMLTableCellElement[]) {
        const cSpan = c.colSpan || 1;
        if (cIdx <= targetCol && targetCol < cIdx + cSpan) {
          if (cSpan > 1 && c !== cell) {
            c.colSpan = cSpan + 1;
          } else {
            const newCell = document.createElement(c.tagName === 'TH' ? 'th' : 'td');
            newCell.style.cssText = 'border:1px solid #ccc;padding:4px 6px;font-size:11px';
            newCell.innerHTML = '&nbsp;';
            c.parentNode?.insertBefore(newCell, c.nextSibling);
          }
          inserted = true;
          break;
        }
        cIdx += cSpan;
      }
      if (!inserted) {
        const newCell = document.createElement('td');
        newCell.style.cssText = 'border:1px solid #ccc;padding:4px 6px;font-size:11px';
        newCell.innerHTML = '&nbsp;';
        r.appendChild(newCell);
      }
    }
    this.onSegmentInput(this.activeSeg());
    this.updateSelectedCellInfo();
  }

  protected addCell(): void {
    const ctx = this.getTableContext();
    if (!ctx) return;
    const { cell } = ctx;
    const newCell = document.createElement('td');
    newCell.style.cssText = 'border:1px solid #ccc;padding:4px 6px;font-size:11px';
    newCell.innerHTML = '&nbsp;';
    cell.parentNode?.insertBefore(newCell, cell.nextSibling);
    this.setActiveCell(newCell);
    this.onSegmentInput(this.activeSeg());
  }

  protected mergeCells(): void {
    const ctx = this.getTableContext();
    if (!ctx) return;
    const { cell } = ctx;
    const next = cell.nextElementSibling as HTMLTableCellElement | null;
    if (!next) return;
    cell.colSpan = (cell.colSpan || 1) + (next.colSpan || 1);
    cell.innerHTML = `${cell.innerHTML.trim()} ${next.innerHTML.trim()}`;
    next.remove();
    this.onSegmentInput(this.activeSeg());
    this.updateSelectedCellInfo();
  }

  protected splitCell(): void {
    const ctx = this.getTableContext();
    if (!ctx) return;
    const { cell } = ctx;
    const span = cell.colSpan || 1;
    if (span <= 1) return;
    cell.colSpan = 1;
    for (let i = 1; i < span; i += 1) {
      const newCell = document.createElement(cell.tagName === 'TH' ? 'th' : 'td');
      newCell.style.cssText = cell.style.cssText;
      newCell.colSpan = 1;
      newCell.innerHTML = '&nbsp;';
      cell.parentNode?.insertBefore(newCell, cell.nextSibling);
    }
    this.onSegmentInput(this.activeSeg());
    this.updateSelectedCellInfo();
  }

  protected delRow(): void {
    const ctx = this.getTableContext();
    if (!ctx) return;
    const { table, row } = ctx;
    row.remove();
    this.setActiveCell(null);
    if (table.rows.length === 0) {
      table.remove();
    }
    this.onSegmentInput(this.activeSeg());
  }

  protected delCol(): void {
    const ctx = this.getTableContext();
    if (!ctx) return;
    const { table, row, cell } = ctx;
    const colIdx = this.getColIndex(row, cell);

    for (const r of Array.from(table.rows)) {
      let cIdx = 0;
      for (const c of Array.from(r.children) as HTMLTableCellElement[]) {
        const cSpan = c.colSpan || 1;
        if (cIdx <= colIdx && colIdx < cIdx + cSpan) {
          if (cSpan > 1) {
            c.colSpan = cSpan - 1;
          } else {
            c.remove();
          }
          break;
        }
        cIdx += cSpan;
      }
    }
    this.setActiveCell(null);
    if (table.rows.length && table.rows[0].children.length === 0) {
      table.remove();
    }
    this.onSegmentInput(this.activeSeg());
  }

  protected delCell(): void {
    const ctx = this.getTableContext();
    if (!ctx) return;
    const { cell } = ctx;
    cell.remove();
    this.setActiveCell(null);
    this.onSegmentInput(this.activeSeg());
  }

  protected toggleBorder(): void {
    const ctx = this.getTableContext();
    if (!ctx) return;
    const { cell } = ctx;
    const current = cell.style.border;
    if (!current || current === 'none') {
      cell.style.border = '1px solid #ccc';
    } else if (current === '1px solid rgb(204, 204, 204)' || current === '1px solid #ccc') {
      cell.style.border = '1px solid #000';
    } else {
      cell.style.border = 'none';
    }
    this.onSegmentInput(this.activeSeg());
  }

  protected toggleShade(): void {
    const ctx = this.getTableContext();
    if (!ctx) return;
    const { cell } = ctx;
    cell.style.background = cell.style.background ? '' : 'rgba(242,97,15,.12)';
    this.onSegmentInput(this.activeSeg());
  }

  protected setColWidth(pct: number): void {
    const ctx = this.getTableContext();
    if (!ctx) return;
    const { table, row, cell } = ctx;
    const colIdx = this.getColIndex(row, cell);

    for (const r of Array.from(table.rows)) {
      let cIdx = 0;
      for (const c of Array.from(r.children) as HTMLTableCellElement[]) {
        const cSpan = c.colSpan || 1;
        if (cIdx <= colIdx && colIdx < cIdx + cSpan) {
          if (cSpan === 1) {
            if (pct > 0) {
              c.style.width = `${pct}%`;
            } else {
              c.style.width = '';
            }
            c.removeAttribute('width');
          }
          break;
        }
        cIdx += cSpan;
      }
    }
    if (pct > 0) {
      cell.style.width = `${pct}%`;
    } else {
      cell.style.width = '';
    }
    cell.removeAttribute('width');

    this.onSegmentInput(this.activeSeg());
    this.updateSelectedCellInfo();
  }

  protected adjustColWidth(delta: number): void {
    const info = this.selectedCellInfo();
    const current = info.colWidthPct || 20;
    const next = Math.max(5, Math.min(100, current + delta));
    this.setColWidth(next);
  }

  protected autoColWidth(): void {
    this.recordSnapshot(true);
    this.setColWidth(0);
    this.showFlash('Column width set to auto');
  }

  protected equalizeCols(): void {
    const ctx = this.getTableContext();
    if (!ctx) return;
    const { table } = ctx;
    this.recordSnapshot(true);

    const maxCols = Math.max(
      ...Array.from(table.rows).map((r) => {
        let count = 0;
        for (const c of Array.from(r.children) as HTMLTableCellElement[]) {
          count += c.colSpan || 1;
        }
        return count;
      }),
    );

    if (maxCols <= 0) return;
    const equalW = Math.round((100 / maxCols) * 10) / 10;

    for (const r of Array.from(table.rows)) {
      for (const c of Array.from(r.children) as HTMLTableCellElement[]) {
        const span = c.colSpan || 1;
        c.style.width = `${Math.round(equalW * span * 10) / 10}%`;
        c.removeAttribute('width');
      }
    }

    this.onSegmentInput(this.activeSeg());
    this.updateSelectedCellInfo();
    this.recordSnapshot(true);
    this.showFlash(`Equalized ${maxCols} columns (${equalW}% each)`);
  }

  protected autoActiveSectionCols(): void {
    const segEl = this.segElements().find(
      (ref) => ref.nativeElement.dataset['seg'] === this.activeSeg(),
    )?.nativeElement;
    if (!segEl) return;
    const table = segEl.querySelector('table');
    if (!table) return;

    this.recordSnapshot(true);
    for (const r of Array.from(table.rows)) {
      for (const c of Array.from(r.children) as HTMLTableCellElement[]) {
        c.style.width = '';
        c.removeAttribute('width');
      }
    }
    this.onSegmentInput(this.activeSeg());
    this.updateSelectedCellInfo();
    this.recordSnapshot(true);
    this.showFlash('Section columns set to auto');
  }

  protected equalizeActiveSectionCols(): void {
    const segEl = this.segElements().find(
      (ref) => ref.nativeElement.dataset['seg'] === this.activeSeg(),
    )?.nativeElement;
    if (!segEl) return;
    const table = segEl.querySelector('table');
    if (!table) return;

    this.recordSnapshot(true);
    const maxCols = Math.max(
      ...Array.from(table.rows).map((r) => {
        let count = 0;
        for (const c of Array.from(r.children) as HTMLTableCellElement[]) {
          count += c.colSpan || 1;
        }
        return count;
      }),
    );

    if (maxCols <= 0) return;
    const equalW = Math.round((100 / maxCols) * 10) / 10;

    for (const r of Array.from(table.rows)) {
      for (const c of Array.from(r.children) as HTMLTableCellElement[]) {
        const span = c.colSpan || 1;
        c.style.width = `${Math.round(equalW * span * 10) / 10}%`;
        c.removeAttribute('width');
      }
    }

    this.onSegmentInput(this.activeSeg());
    this.updateSelectedCellInfo();
    this.recordSnapshot(true);
    this.showFlash(`Equalized ${maxCols} columns (${equalW}% each)`);
  }

  // ---- Mouse Drag & Drop Column Alignment -----------------------------------

  protected onCellHover(e: MouseEvent, segCode: SegCode): void {
    if (this.colResizeState() || !this.canEdit() || this.mode() !== 'edit') return;
    const target = e.target as HTMLElement;
    const cell = target.closest('td, th') as HTMLTableCellElement | null;
    if (!cell) {
      document.body.style.cursor = '';
      return;
    }
    const rect = cell.getBoundingClientRect();
    const distFromRight = rect.right - e.clientX;
    const nextCell = cell.nextElementSibling as HTMLTableCellElement | null;
    if (distFromRight >= 0 && distFromRight <= 8 && nextCell) {
      cell.style.cursor = 'col-resize';
    } else {
      cell.style.cursor = '';
    }
  }

  protected onCellMouseDown(e: MouseEvent, segCode: SegCode): void {
    if (!this.canEdit() || this.mode() !== 'edit') return;
    const target = e.target as HTMLElement;
    const cell = target.closest('td, th') as HTMLTableCellElement | null;
    if (!cell) return;
    const rect = cell.getBoundingClientRect();
    const distFromRight = rect.right - e.clientX;
    const nextCell = cell.nextElementSibling as HTMLTableCellElement | null;
    if (distFromRight >= 0 && distFromRight <= 8 && nextCell) {
      const table = cell.closest('table');
      const row = cell.closest('tr');
      if (!table || !row) return;

      e.preventDefault();
      e.stopPropagation();

      const colIdx = this.getColIndex(row, cell);
      const z = this.zoom();
      const tableRect = table.getBoundingClientRect();
      const c1W = cell.getBoundingClientRect().width / z;
      const c2W = nextCell.getBoundingClientRect().width / z;
      const totalW = tableRect.width / z;
      const p1 = Math.round((c1W / totalW) * 1000) / 10;
      const p2 = Math.round((c2W / totalW) * 1000) / 10;

      this.colResizeState.set({
        segCode,
        table,
        colIndex: colIdx,
        startX: e.clientX,
        currentX: e.clientX,
        startW1: c1W,
        startW2: c2W,
        totalW,
        startPct1: p1,
        startPct2: p2,
        curPct1: p1,
        curPct2: p2,
        top: tableRect.top,
        bottom: tableRect.bottom,
      });
      document.body.style.cursor = 'col-resize';
    }
  }

  @HostListener('window:mousemove', ['$event'])
  protected handleMouseMove(e: MouseEvent): void {
    const st = this.colResizeState();
    if (!st) return;

    const z = this.zoom();
    const deltaPx = (e.clientX - st.startX) / z;
    const sumPct = st.startPct1 + st.startPct2;

    const deltaPct = (deltaPx / st.totalW) * 100;
    const newPct1 = Math.max(3, Math.min(sumPct - 3, Math.round((st.startPct1 + deltaPct) * 10) / 10));
    const newPct2 = Math.round((sumPct - newPct1) * 10) / 10;

    this.colResizeState.set({
      ...st,
      currentX: e.clientX,
      curPct1: newPct1,
      curPct2: newPct2,
    });
  }

  @HostListener('window:mouseup')
  protected handleMouseUp(): void {
    const st = this.colResizeState();
    if (!st) return;

    this.recordSnapshot(true);
    for (const r of Array.from(st.table.rows)) {
      let cIdx = 0;
      for (const c of Array.from(r.children) as HTMLTableCellElement[]) {
        const span = c.colSpan || 1;
        if (cIdx === st.colIndex && span === 1) {
          c.style.width = `${st.curPct1}%`;
          c.removeAttribute('width');
        } else if (cIdx === st.colIndex + 1 && span === 1) {
          c.style.width = `${st.curPct2}%`;
          c.removeAttribute('width');
        }
        cIdx += span;
      }
    }

    document.body.style.cursor = '';
    this.colResizeState.set(null);
    this.onSegmentInput(st.segCode);
    this.updateSelectedCellInfo();
    this.recordSnapshot(true);
    this.showFlash(`Column width adjusted: ${st.curPct1}% / ${st.curPct2}%`);
  }

  protected setRowHeight(px: number): void {
    const ctx = this.getTableContext();
    if (!ctx) return;
    const { row } = ctx;
    if (px > 0) {
      row.style.height = `${px}px`;
    } else {
      row.style.height = '';
    }
    row.removeAttribute('height');
    this.onSegmentInput(this.activeSeg());
    this.updateSelectedCellInfo();
  }

  protected adjustRowHeight(delta: number): void {
    const info = this.selectedCellInfo();
    const current = info.rowHeightPx || 24;
    const next = Math.max(12, Math.min(300, current + delta));
    this.setRowHeight(next);
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
    this.recordSnapshot(true);
    const chipHtml = `<span contenteditable="false" class="pt-chip${isList ? ' pt-chip--list' : ''}">${isList ? `&lt;&lt;${tag}&gt;&gt;` : `{{${tag}}}`}</span>\u00a0`;
    this.execSel('insertHTML', chipHtml);
    this.fieldsOpen.set(false);
    this.onSegmentInput(this.activeSeg());
    this.recordSnapshot(true);
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

  protected async printPreview(): Promise<void> {
    this.bankContentFromDom();
    this.printClientPreview();
  }

  private printClientPreview(): void {
    const s = this.draftSettings();
    const isLaser = (s.printerType ?? 'laser') === 'laser';
    const paper = (s.paperSize ?? 'A4') as PaperSize;
    const rollW = s.rollWidthMm ?? 80;
    const font = this.pageFontFamily() || this.lastFont() || 'Arial';
    const sz = this.pageFontSize() || this.currentFontSize() || 11;

    const pageCss = isLaser
      ? `@page { size: ${paper} portrait; margin: ${s.marginTopMm ?? 16}mm ${s.marginRightMm ?? 18}mm ${s.marginBottomMm ?? 16}mm ${s.marginLeftMm ?? 18}mm; }`
      : `@page { size: ${rollW}mm auto; margin: ${s.marginTopMm ?? 2}mm ${s.marginRightMm ?? 2}mm ${s.marginBottomMm ?? 2}mm ${s.marginLeftMm ?? 2}mm; }`;

    const fhHtml = this.ptMockHtml(this.tagToChip(this.getSegmentTagHtml('fh')));
    const hHtml = this.ptMockHtml(this.tagToChip(this.getSegmentTagHtml('h')));
    const dHtml = this.ptMockHtml(this.tagToChip(this.getSegmentTagHtml('d')));
    const fHtml = this.ptMockHtml(this.tagToChip(this.getSegmentTagHtml('f')));
    const ffHtml = this.ptMockHtml(this.tagToChip(this.getSegmentTagHtml('ff')));

    const fhMargin = this.getSegMarginStyle('fh');
    const hMargin = this.getSegMarginStyle('h');
    const dMargin = this.getSegMarginStyle('d');
    const fMargin = this.getSegMarginStyle('f');
    const ffMargin = this.getSegMarginStyle('ff');

    const html = `<!DOCTYPE html>
<html>
<head>
<meta charset="utf-8">
<title>${this.draftName() || 'Print Preview'}</title>
<style>
${pageCss}
* { box-sizing: border-box; }
body {
  font-family: ${font}, -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
  font-size: ${sz}px;
  line-height: 1.35;
  margin: 0;
  padding: 0;
  color: #201f1d;
  background: #fff;
  -webkit-print-color-adjust: exact;
  print-color-adjust: exact;
}
table {
  border-collapse: collapse;
  width: 100%;
}
th, td {
  padding: 4px 6px;
  vertical-align: top;
  font-family: inherit;
  font-size: inherit;
}
.pt-print-block {
  display: block;
  width: 100%;
}
.pt-fh { margin: ${fhMargin}; }
.pt-h  { margin: ${hMargin}; }
.pt-d  { margin: ${dMargin}; }
.pt-f  { margin: ${fMargin}; }
.pt-ff { margin: ${ffMargin}; }
@media print {
  body { margin: 0; padding: 0; }
  .pt-print-block { page-break-inside: avoid; }
}
</style>
</head>
<body>
  ${fhHtml ? `<div class="pt-print-block pt-fh">${fhHtml}</div>` : ''}
  ${hHtml ? `<div class="pt-print-block pt-h">${hHtml}</div>` : ''}
  ${dHtml ? `<div class="pt-print-block pt-d">${dHtml}</div>` : ''}
  ${fHtml ? `<div class="pt-print-block pt-f">${fHtml}</div>` : ''}
  ${ffHtml ? `<div class="pt-print-block pt-ff">${ffHtml}</div>` : ''}
</body>
</html>`;

    printHtml(html, isLaser ? 'document' : 'receipt');
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
    const clean = tag.replace(/&lt;/g, '<').replace(/&gt;/g, '>').replace(/[↻\s«»{}<>]/g, '').trim();
    const docType = this.documentType() ?? 'INV';

    // Document-specific overrides
    if (clean === 'Party.Name' || clean === 'Party.PartyName' || clean === 'Party.CustomerName' || clean === 'Party.VendorName' || clean === 'Customer.Name' || clean === 'Vendor.Name') {
      return ['POR', 'BIL', 'GRN', 'DBN', 'SPM'].includes(docType)
        ? 'Sundar Steel Works Pvt Ltd'
        : 'Halcyon Interiors Pvt Ltd';
    }
    if (clean === 'Party.Address' || clean === 'Party.BillingAddress' || clean === 'Party.PartyAddress' || clean === 'Customer.Address' || clean === 'Vendor.Address') {
      return ['POR', 'BIL', 'GRN', 'DBN', 'SPM'].includes(docType)
        ? 'Plot 42, MIDC Phase II, Chakan Industrial Area<br>Pune, Maharashtra 411026'
        : '3rd Floor, Prestige Atrium, Residency Road<br>Bengaluru, Karnataka 560025';
    }
    if (clean === 'Party.ShippingAddress' || clean === 'Party.DeliveryAddress') {
      return ['POR', 'BIL', 'GRN', 'DBN', 'SPM'].includes(docType)
        ? '14 Fort Road, Ballard Estate<br>Mumbai, Maharashtra 400001'
        : 'Site 42, Outer Ring Road, Marathahalli<br>Bengaluru, Karnataka 560037';
    }
    if (clean === 'Party.Gstin' || clean === 'Party.GSTIN' || clean === 'Party.GstNo' || clean === 'Customer.GSTIN' || clean === 'Vendor.GSTIN') {
      return ['POR', 'BIL', 'GRN', 'DBN', 'SPM'].includes(docType)
        ? '27AAFCS4471M1Z2'
        : '29AACCH8821P1ZB';
    }
    if (clean === 'Party.StateName' || clean === 'Party.State') {
      return ['POR', 'BIL', 'GRN', 'DBN', 'SPM'].includes(docType)
        ? 'Maharashtra'
        : 'Karnataka';
    }
    if (clean === 'Party.Phone' || clean === 'Party.Mobile') {
      return ['POR', 'BIL', 'GRN', 'DBN', 'SPM'].includes(docType)
        ? '+91 20 2712 4400'
        : '+91 80 4123 9900';
    }
    if (clean === 'Party.Email' || clean === 'Party.EmailAddress') {
      return ['POR', 'BIL', 'GRN', 'DBN', 'SPM'].includes(docType)
        ? 'sales@sundarsteel.example.com'
        : 'accounts@halcyoninteriors.com';
    }
    if (clean === 'Document.No' || clean === 'Document.DocNo' || clean === 'InvoiceNo') {
      switch (docType) {
        case 'QTE': return 'QOT/2026/00319';
        case 'SOR': return 'SOR/2026/00943';
        case 'DLC': return 'DC/2026/00731';
        case 'CRN': return 'CN/2026/00042';
        case 'POR': return 'PO/2026/00219';
        case 'GRN': return 'GRN/2026/00589';
        case 'BIL': return 'BIL/2026/01207';
        case 'DBN': return 'DBN/2026/00098';
        case 'RCM': return 'REC/2026/00812';
        case 'SPM': return 'PAY/2026/00477';
        case 'JRN': return 'JV/2026/00319';
        case 'KOT': return 'KOT-084';
        default: return 'SI/2026/00418';
      }
    }
    if (clean === 'Document.Reference' || clean === 'Document.RefNo' || clean === 'PONumber') {
      switch (docType) {
        case 'INV': return 'PO/2026/00219';
        case 'BIL': return 'SSW/26/894';
        case 'CRN': return 'SI/2026/00311';
        case 'DBN': return 'BIL/2026/01207';
        case 'DLC': return 'SOR/2026/00943';
        case 'GRN': return 'PO/2026/00219';
        case 'RCM': return 'UTR 402188173';
        case 'SPM': return 'CMS 990142881';
        default: return 'PO/2026/00219';
      }
    }
    if (clean === 'Document.OriginalDocumentNo' || clean === 'Document.OrigDocNo' || clean === 'AgainstInvoiceNo' || clean === 'AgainstDocNo') {
      return docType === 'DBN' ? 'BIL/2026/01207' : 'SI/2026/00311';
    }
    if (clean === 'Document.Reason' || clean === 'Reason') {
      return docType === 'DBN'
        ? 'Goods rejected on quality inspection'
        : 'Short supply against original invoice';
    }

    // List fields case-insensitive lookup
    const listKey = Object.keys(MOCK_LIST).find((k) => k.toLowerCase() === clean.toLowerCase());
    if (listKey) {
      const listGen = MOCK_LIST[listKey];
      return typeof listGen === 'function' ? listGen(i, docType) : listGen[i % listGen.length];
    }

    // Single fields exact lookup
    if (MOCK_SINGLE[clean] != null) {
      return MOCK_SINGLE[clean];
    }

    // Single fields case-insensitive lookup
    const singleKey = Object.keys(MOCK_SINGLE).find((k) => k.toLowerCase() === clean.toLowerCase());
    if (singleKey) {
      return MOCK_SINGLE[singleKey];
    }

    // Custom field fallback
    if (clean.toLowerCase().startsWith('custom.')) {
      return `Sample ${clean.substring(7)}`;
    }

    return clean;
  }

  protected ptMockHtml(html: string): string {
    const root = document.createElement('div');
    root.innerHTML = html || '';
    const tagOf = (text: string) =>
      text
        .replace(/&lt;/g, '<')
        .replace(/&gt;/g, '>')
        .replace(/[↻\s«»{}<>]/g, '')
        .trim();

    // 1. Expand repeating table rows for list tags
    Array.from(root.querySelectorAll('tr')).forEach((tr) => {
      if (tr.querySelector('tr')) return;

      let listTag: string | null = null;
      const chips = Array.from(tr.querySelectorAll('.pt-chip'));
      for (const chip of chips) {
        const t = tagOf(chip.textContent || '');
        if (chip.classList.contains('pt-chip--list') || this.isListTag(t)) {
          listTag = t;
          break;
        }
      }

      if (!listTag) {
        const trHtml = tr.innerHTML || '';
        const match = trHtml.match(/(?:\{\{|<<|«|&lt;&lt;)\s*([^{}<>«»&]+?)\s*(?:\}\}|>>|»|&gt;&gt;)/i);
        if (match) {
          const t = tagOf(match[1]);
          if (this.isListTag(t)) {
            listTag = t;
          }
        }
      }

      if (!listTag) return;
      const group = listTag.split('.')[0] || 'Item';
      const n = MOCK_ROWS[group] || (this.documentType() === 'KOT' ? 5 : 4);
      tr.setAttribute('data-mock-i', '0');
      let anchor = tr;
      for (let i = 1; i < n; i++) {
        const c = tr.cloneNode(true) as HTMLTableRowElement;
        c.setAttribute('data-mock-i', String(i));
        anchor.insertAdjacentElement('afterend', c);
        anchor = c;
      }
    });

    // 2. Replace all .pt-chip elements with mock values
    Array.from(root.querySelectorAll('.pt-chip')).forEach((chip) => {
      const row = chip.closest('tr[data-mock-i]');
      const tag = tagOf(chip.textContent || '');
      const rowIdx = row ? Number(row.getAttribute('data-mock-i')) || 0 : 0;
      const val = this.mockVal(tag, rowIdx);
      if (typeof val === 'string' && val.includes('<') && val.includes('>')) {
        const span = document.createElement('span');
        span.innerHTML = val;
        chip.replaceWith(span);
      } else {
        chip.replaceWith(document.createTextNode(val));
      }
    });

    // 3. Fallback pass: walk all text nodes in root and replace unchipped raw tags
    const tagRegex = /(?:\{\{|<<|«|&lt;&lt;)\s*([^{}<>«»&]+?)\s*(?:\}\}|>>|»|&gt;&gt;)/gi;
    const walkTextNodes = (node: Node) => {
      if (node.nodeType === Node.TEXT_NODE) {
        const text = node.nodeValue || '';
        if (tagRegex.test(text)) {
          tagRegex.lastIndex = 0;
          const row = (node.parentElement as HTMLElement)?.closest('tr[data-mock-i]');
          const rowIdx = row ? Number(row.getAttribute('data-mock-i')) || 0 : 0;

          let hasMarkup = false;
          const replaced = text.replace(tagRegex, (_match, raw) => {
            const val = this.mockVal(tagOf(raw), rowIdx);
            if (typeof val === 'string' && val.includes('<') && val.includes('>')) {
              hasMarkup = true;
            }
            return val;
          });

          if (hasMarkup) {
            const span = document.createElement('span');
            span.innerHTML = replaced;
            node.parentNode?.replaceChild(span, node);
          } else {
            node.nodeValue = replaced;
          }
        }
      } else {
        for (const child of Array.from(node.childNodes)) {
          walkTextNodes(child);
        }
      }
    };

    walkTextNodes(root);

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
