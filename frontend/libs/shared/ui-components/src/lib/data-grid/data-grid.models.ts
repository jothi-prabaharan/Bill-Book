export interface ColumnDef {
  field: string;
  header: string;
  align?: string;
  type?: string;
  isTemplate?: boolean;
  title?: string;
  classes?: string;
  dataType?: 'string' | 'number' | 'date' | 'datetime' | 'boolean' | 'money' | 'quantity' | 'unitprice' | 'status';
  width?: string;
  visible?: boolean;
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
}
