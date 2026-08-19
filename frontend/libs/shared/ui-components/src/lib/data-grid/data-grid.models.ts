export interface ColumnDef {
  field: string;
  header?: string;
  title?: string;
  align?: 'left' | 'right' | 'center' | string;
  type?: string;
  isTemplate?: boolean;
  classes?: string;
  dataType?: 'string' | 'number' | 'date' | 'datetime' | 'boolean' | 'money' | 'quantity' | 'unitprice' | 'status';
  width?: string;
  visible?: boolean;
  numeric?: boolean;
  sortable?: boolean;
}

export type SortDirection = 'asc' | 'desc';

export interface SortState {
  field: string;
  direction: SortDirection;
}

export interface FilterState {
  field: string;
  operator: 'equals' | 'contains' | 'starts';
  value: string;
}

export interface GridState {
  gridCode: string;
  columns: { field: string; visible: boolean; width?: string }[];
  filters: FilterState[];
  pageSize: number;
  sort?: SortState;
}
