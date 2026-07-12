import { KpiReference } from '../types/api';

export interface FormattedKpi {
  label: string;
  formattedValue: string;
  table?: string;
}

/**
 * metric_name / metric_value ペアを「指標名: 値」に結合し、
 * 通常 KPI はそのまま logicalNameJa → physicalName で表示する。
 */
export function formatKpiPairs(kpis: KpiReference[]): FormattedKpi[] {
  const result: FormattedKpi[] = [];
  const metricColumns = new Set(['metric_name', 'metric_value', 'metric_unit', 'description', 'year_month']);

  let pendingName: string | null = null;
  let pendingTable: string | undefined;

  for (const kpi of kpis) {
    const key = (kpi.physicalName ?? '').toLowerCase();

    if (key === 'metric_name') {
      // Flush any previous pending name
      if (pendingName) {
        result.push({ label: pendingName, formattedValue: '', table: pendingTable });
      }
      pendingName = kpi.value ?? kpi.logicalNameJa ?? '';
      pendingTable = kpi.table;
      continue;
    }

    if (key === 'metric_value' && pendingName) {
      const val = kpi.value ?? '';
      const unit = kpi.unit ?? '';
      result.push({
        label: pendingName,
        formattedValue: val ? `${val}${unit}` : '',
        table: pendingTable ?? kpi.table,
      });
      pendingName = null;
      pendingTable = undefined;
      continue;
    }

    // Skip other table-structure columns
    if (metricColumns.has(key)) continue;

    // Normal KPI entry
    const label = kpi.logicalNameJa || kpi.physicalName;
    const val = kpi.value ?? '';
    const unit = kpi.unit ?? '';
    result.push({
      label,
      formattedValue: val ? `${val}${unit}` : '',
      table: kpi.table,
    });
  }

  // Flush any remaining pending name
  if (pendingName) {
    result.push({ label: pendingName, formattedValue: '', table: pendingTable });
  }

  return result;
}
