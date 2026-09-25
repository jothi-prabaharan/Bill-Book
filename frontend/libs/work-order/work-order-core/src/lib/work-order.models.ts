/** The work order service's shapes (S6, TK-66), as the API sends them: enums by name. */

export type WorkOrderSource = 'Complaint' | 'Preventive' | 'Amc' | 'Inspection';
export type WorkOrderPriority = 'Low' | 'Medium' | 'High' | 'Urgent';
export type WorkOrderStatus = 'Open' | 'Assigned' | 'InProgress' | 'OnHold' | 'Completed' | 'Closed' | 'Cancelled';
export type WorkOrderAction = 'Assign' | 'Start' | 'Hold' | 'Resume' | 'Complete' | 'Close' | 'Cancel';

export interface WorkOrderTask {
  workOrderTaskId: number;
  description: string;
  isDone: boolean;
}

export interface WorkOrderPart {
  workOrderPartId: number;
  itemId: number;
  itemName: string | null;
  warehouseId: number | null;
  quantity: number;
  unitCost: number;
}

export interface WorkOrder {
  workOrderId: number;
  workOrderNo: string;
  title: string;
  description: string | null;
  workOrderSource: WorkOrderSource;
  priority: WorkOrderPriority;
  facilityAssetId: number | null;
  spaceId: number | null;
  reportedDate: string;
  dueDate: string | null;
  assignedEmployeeId: number | null;
  amcContractId: number | null;
  preventivePlanId: number | null;
  workOrderStatus: WorkOrderStatus;
  completedDate: string | null;
  labourCost: number;
  partsCost: number;
  cancelReason: string | null;
  tasks: WorkOrderTask[];
  parts: WorkOrderPart[];
}

export interface SaveWorkOrder {
  title: string;
  description: string | null;
  workOrderSource: WorkOrderSource;
  priority: WorkOrderPriority;
  facilityAssetId: number | null;
  spaceId: number | null;
  reportedDate: string;
  dueDate: string | null;
  tasks: string[];
}

export interface WorkOrderActionBody {
  action: WorkOrderAction;
  employeeId?: number | null;
  completedDate?: string | null;
  labourCost?: number | null;
  reason?: string | null;
}

export interface IssuePart {
  itemId: number;
  warehouseId: number | null;
  quantity: number;
  issueDate: string;
}

export interface StockItem {
  itemId: number;
  itemCode: string;
  itemName: string;
  quantityOnHand: number;
}

export interface StockWarehouse {
  warehouseId: number;
  warehouseCode: string;
  warehouseName: string;
}

export const WORK_ORDER_STATUSES: readonly { value: WorkOrderStatus; label: string }[] = [
  { value: 'Open', label: 'Open' },
  { value: 'Assigned', label: 'Assigned' },
  { value: 'InProgress', label: 'In progress' },
  { value: 'OnHold', label: 'On hold' },
  { value: 'Completed', label: 'Completed' },
  { value: 'Closed', label: 'Closed' },
  { value: 'Cancelled', label: 'Cancelled' },
];

export const WORK_ORDER_PRIORITIES: readonly { value: WorkOrderPriority; label: string }[] = [
  { value: 'Low', label: 'Low' },
  { value: 'Medium', label: 'Medium' },
  { value: 'High', label: 'High' },
  { value: 'Urgent', label: 'Urgent' },
];

export const WORK_ORDER_SOURCES: readonly { value: WorkOrderSource; label: string }[] = [
  { value: 'Complaint', label: 'Complaint' },
  { value: 'Preventive', label: 'Preventive plan' },
  { value: 'Amc', label: 'AMC visit' },
  { value: 'Inspection', label: 'Inspection' },
];

/**
 * The moves a work order offers from where it stands — the same table the
 * server enforces, so a screen never offers a button the server will refuse.
 * Close is left out: it has its own button and its own permission.
 */
export function nextActions(status: WorkOrderStatus): WorkOrderAction[] {
  switch (status) {
    case 'Open':
      return ['Assign', 'Cancel'];
    case 'Assigned':
      return ['Assign', 'Start', 'Cancel'];
    case 'InProgress':
      return ['Hold', 'Complete'];
    case 'OnHold':
      return ['Resume'];
    default:
      return [];
  }
}

/** Only an Open work order can be edited; parts are issued while the work is under way. */
export const isEditable = (status: WorkOrderStatus): boolean => status === 'Open';
export const takesParts = (status: WorkOrderStatus): boolean => status === 'Assigned' || status === 'InProgress' || status === 'OnHold';
