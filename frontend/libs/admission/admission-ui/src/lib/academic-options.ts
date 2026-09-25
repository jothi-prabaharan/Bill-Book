import { BbSelectOption } from '@bill-book/ui-components';
import { SisApiService } from '@bill-book/sis-core';

/** The school years (open ones, current first) and active classes, for the admission forms' pickers. */
export async function academicOptions(sis: SisApiService): Promise<{
  years: BbSelectOption<number>[];
  classes: BbSelectOption<number>[];
  currentYearId: number | null;
}> {
  const [years, classes] = await Promise.all([sis.years(), sis.classes()]);
  const open = [...years].filter((y) => !y.isClosed).sort((a, b) => Number(b.isCurrent) - Number(a.isCurrent));
  return {
    years: open.map((y) => ({ value: y.academicYearId, label: y.code })),
    classes: classes.filter((c) => c.isActive).map((c) => ({ value: c.schoolClassId, label: c.name })),
    currentYearId: open.find((y) => y.isCurrent)?.academicYearId ?? null,
  };
}
