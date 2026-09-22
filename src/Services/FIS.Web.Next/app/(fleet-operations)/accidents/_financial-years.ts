/** South African book years, April–March, newest first. */
export function accidentFinancialYears(reference = new Date()): string[] {
  const calendarYear = reference.getFullYear();
  const fyStartYear = reference.getMonth() >= 3 ? calendarYear : calendarYear - 1;
  const years: string[] = [];
  for (let offset = -1; offset <= 10; offset += 1) {
    const startYear = fyStartYear - offset;
    years.push(
      `${String(startYear).slice(-2).padStart(2, "0")}/${String(startYear + 1)
        .slice(-2)
        .padStart(2, "0")}`,
    );
  }
  return years;
}

export function accidentFinancialYearChoices(existing?: string | null): string[] {
  const years = accidentFinancialYears();
  const trimmed = existing?.trim();
  if (trimmed && !years.includes(trimmed)) {
    return [trimmed, ...years];
  }
  return years;
}
