/**
 * Models and enums for the Site Services module (TK-168/TK-169).
 */

export type SiteStatus = 'Draft' | 'Active' | 'OnHold' | 'Completed' | 'Closed';
export type AssignmentStatus = 'Assigned' | 'Active' | 'Relieved' | 'Terminated';
export type MarginScope = 'Branch' | 'Site' | 'Trade' | 'Item' | 'Assignment';
export type MarginAppliesTo = 'All' | 'Labor' | 'Material';
export type MarginType = 'FlatPerUnit' | 'Percent';

export interface SiteDto {
  siteId: number;
  siteCode: string;
  siteName: string;
  clientContactId: number;
  clientContactName?: string;
  projectManagerUserId?: string | null;
  status: SiteStatus;
  warehouseId?: number | null;
  addressLine1?: string | null;
  city?: string | null;
  state?: string | null;
  pincode?: string | null;
  latitude?: number | null;
  longitude?: number | null;
  geofenceRadiusMeters: number;
  startDate?: string | null;
  targetEndDate?: string | null;
  actualEndDate?: string | null;
  notes?: string | null;
}

export interface SaveSiteDto {
  siteName: string;
  clientContactId: number;
  projectManagerUserId?: string | null;
  status: SiteStatus;
  addressLine1?: string | null;
  city?: string | null;
  state?: string | null;
  pincode?: string | null;
  latitude?: number | null;
  longitude?: number | null;
  geofenceRadiusMeters: number;
  startDate?: string | null;
  targetEndDate?: string | null;
  notes?: string | null;
}
