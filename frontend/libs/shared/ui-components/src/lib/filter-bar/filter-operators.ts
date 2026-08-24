import { ColumnDataType, FilterOperator } from '@bill-book/reporting-core';

/**
 * Which operators a column offers, and how many values each needs.
 *
 * **Pure and separate from the component**, because this is the part with a wrong
 * answer: offering `Contains` on a date produces a filter the server refuses, and
 * offering `GreaterThan` on text produces one it accepts and answers strangely —
 * "Cash" > "Bank" is true, alphabetically, which is not what anyone meant.
 */

const TEXT: FilterOperator[] = [
  'Contains',
  'NotContains',
  'Equals',
  'NotEquals',
  'StartsWith',
  'EndsWith',
  'In',
  'NotIn',
  'IsNull',
  'IsNotNull',
];

const ORDERED: FilterOperator[] = [
  'Equals',
  'NotEquals',
  'GreaterThan',
  'GreaterOrEqual',
  'LessThan',
  'LessOrEqual',
  'Between',
  'IsNull',
  'IsNotNull',
];

const ENUMERATED: FilterOperator[] = ['In', 'NotIn', 'Equals', 'NotEquals', 'IsNull', 'IsNotNull'];

const BOOLEAN: FilterOperator[] = ['Equals', 'IsNull', 'IsNotNull'];

export function operatorsFor(dataType: ColumnDataType): FilterOperator[] {
  switch (dataType) {
    case 'Text':
    case 'Link':
      return TEXT;

    case 'Number':
    case 'Money':
    case 'Quantity':
    case 'Percent':
    case 'Rate':
    case 'Date':
    case 'DateTime':
      return ORDERED;

    case 'Enum':
      return ENUMERATED;

    case 'Boolean':
      return BOOLEAN;

    default:
      return TEXT;
  }
}

/** How many value boxes an operator needs: none, one, two, or a list. */
export type OperatorArity = 'none' | 'one' | 'two' | 'list';

export function arityOf(operator: FilterOperator): OperatorArity {
  switch (operator) {
    case 'IsNull':
    case 'IsNotNull':
      return 'none';

    case 'Between':
      return 'two';

    case 'In':
    case 'NotIn':
      return 'list';

    default:
      return 'one';
  }
}

/** The `<input type>` for a column, so a date filter gets a date picker. */
export function inputTypeFor(dataType: ColumnDataType): string {
  switch (dataType) {
    case 'Date':
      return 'date';
    case 'DateTime':
      return 'datetime-local';
    case 'Number':
    case 'Money':
    case 'Quantity':
    case 'Percent':
    case 'Rate':
      return 'number';
    default:
      return 'text';
  }
}

/** How an applied filter reads on its chip. */
export function describeOperator(operator: FilterOperator): string {
  const words: Record<FilterOperator, string> = {
    Equals: 'is',
    NotEquals: 'is not',
    Contains: 'contains',
    NotContains: 'does not contain',
    StartsWith: 'starts with',
    EndsWith: 'ends with',
    GreaterThan: '>',
    GreaterOrEqual: '≥',
    LessThan: '<',
    LessOrEqual: '≤',
    Between: 'between',
    In: 'is one of',
    NotIn: 'is not one of',
    IsNull: 'is empty',
    IsNotNull: 'is not empty',
  };

  return words[operator];
}
