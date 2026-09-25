import { BbSelectOption } from '@bill-book/ui-components';
import { StudentApiService } from '@bill-book/student-core';

/** The school years (open ones, current first) and active classes, for the admission forms' pickers. */
export async function academicOptions(studentApi: StudentApiService): Promise<{
  years: BbSelectOption<number>[];
  classes: BbSelectOption<number>[];
  currentYearId: number | null;
}> {
  const [years, classes] = await Promise.all([studentApi.years(), studentApi.classes()]);
  const open = [...years].filter((y) => !y.isClosed).sort((a, b) => Number(b.isCurrent) - Number(a.isCurrent));
  return {
    years: open.map((y) => ({ value: y.academicYearId, label: y.code })),
    classes: classes.filter((c) => c.isActive).map((c) => ({ value: c.schoolClassId, label: c.name })),
    currentYearId: open.find((y) => y.isCurrent)?.academicYearId ?? null,
  };
}
