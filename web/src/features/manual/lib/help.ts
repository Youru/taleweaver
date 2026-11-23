/** The section of the manual that explains each page: the help menu opens it there. */
const sectionOf: Record<string, string> = {
  home: 'overview',
  play: 'encounter',
  table: 'table',
  rules: 'rules',
  workshop: 'workshop',
  'workshop-editor': 'workshop',
  'workshop-print': 'workshop',
}

export function helpSection(routeName: unknown): string | undefined {
  return typeof routeName === 'string' ? sectionOf[routeName] : undefined
}
