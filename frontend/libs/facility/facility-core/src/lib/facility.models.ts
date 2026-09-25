/** The facility service's shapes (S5, TK-65), as the API sends them: enums by name. */

export type SpaceKind = 'Classroom' | 'Laboratory' | 'Office' | 'Toilet' | 'Hall' | 'Playground' | 'Store' | 'Other';
export type AssetCategory = 'Electrical' | 'Plumbing' | 'Hvac' | 'Furniture' | 'It' | 'Lab' | 'Vehicle' | 'Civil' | 'Other';
export type AssetStatus = 'InUse' | 'UnderRepair' | 'Idle' | 'Disposed';

export interface Building {
  buildingId: number;
  code: string;
  name: string;
  floors: number;
  isActive: boolean;
  spaces: number;
}

export type SaveBuilding = Omit<Building, 'buildingId' | 'spaces'>;

export interface Space {
  spaceId: number;
  buildingId: number;
  buildingName: string;
  code: string;
  name: string;
  spaceKind: SpaceKind;
  floor: number;
  capacity: number | null;
  isActive: boolean;
}

export type SaveSpace = Omit<Space, 'spaceId' | 'buildingName'>;

export interface FacilityAsset {
  facilityAssetId: number;
  assetTag: string;
  name: string;
  assetCategory: AssetCategory;
  spaceId: number | null;
  spaceName: string | null;
  buildingName: string | null;
  make: string | null;
  model: string | null;
  serialNo: string | null;
  purchaseDate: string | null;
  warrantyUntil: string | null;
  purchaseCost: number | null;
  assetStatus: AssetStatus;
  isUnderWarranty: boolean;
}

export type SaveAsset = Omit<FacilityAsset, 'facilityAssetId' | 'spaceName' | 'buildingName' | 'isUnderWarranty'>;

export const SPACE_KINDS: readonly { value: SpaceKind; label: string }[] = [
  { value: 'Classroom', label: 'Classroom' },
  { value: 'Laboratory', label: 'Laboratory' },
  { value: 'Office', label: 'Office' },
  { value: 'Toilet', label: 'Toilet' },
  { value: 'Hall', label: 'Hall' },
  { value: 'Playground', label: 'Playground' },
  { value: 'Store', label: 'Store' },
  { value: 'Other', label: 'Other' },
];

export const ASSET_CATEGORIES: readonly { value: AssetCategory; label: string }[] = [
  { value: 'Electrical', label: 'Electrical' },
  { value: 'Plumbing', label: 'Plumbing' },
  { value: 'Hvac', label: 'HVAC' },
  { value: 'Furniture', label: 'Furniture' },
  { value: 'It', label: 'IT' },
  { value: 'Lab', label: 'Lab' },
  { value: 'Vehicle', label: 'Vehicle' },
  { value: 'Civil', label: 'Civil' },
  { value: 'Other', label: 'Other' },
];

export const ASSET_STATUSES: readonly { value: AssetStatus; label: string }[] = [
  { value: 'InUse', label: 'In use' },
  { value: 'UnderRepair', label: 'Under repair' },
  { value: 'Idle', label: 'Idle' },
  { value: 'Disposed', label: 'Disposed' },
];

/** A floor's name: ground, first, basement. */
export function floorLabel(floor: number): string {
  if (floor === 0) {
    return 'Ground';
  }

  return floor < 0 ? `Basement ${-floor}` : `Floor ${floor}`;
}
