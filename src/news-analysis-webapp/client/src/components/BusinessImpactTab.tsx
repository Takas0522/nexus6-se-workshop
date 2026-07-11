import { BusinessImpactResult } from '../types/api';

interface Props {
  data: BusinessImpactResult;
}

const riskColors: Record<string, string> = {
  High: 'bg-red-100 text-red-800 border-red-200',
  Medium: 'bg-amber-100 text-amber-800 border-amber-200',
  Low: 'bg-green-100 text-green-800 border-green-200',
};

const divisionLabels: Record<string, string> = {
  携帯電話事業: '携帯電話事業',
  sns事業: 'SNS事業',
  si事業: 'SI事業',
  // Legacy fallback
  Mobile: 'モバイル通信',
  Ecommerce: 'Eコマース',
  Fintech: 'フィンテック',
};

export default function BusinessImpactTab({ data }: Props) {
  return (
    <div className="space-y-5">
      {/* Impact Scores */}
      <section>
        <h3 className="text-sm font-semibold text-gray-500 uppercase tracking-wide mb-2">
          事業インパクトスコア
        </h3>
        <div className="grid grid-cols-3 gap-3">
          {data.impactScores.map((score) => (
            <div key={score.division} className="bg-white rounded-xl border border-gray-200 p-4 text-center">
              <p className="text-xs text-gray-500 mb-1">{divisionLabels[score.division] ?? score.division}</p>
              <p className="text-3xl font-extrabold text-gray-900">{score.score.toFixed(1)}</p>
              <span className={`inline-block mt-2 text-xs font-semibold px-2.5 py-0.5 rounded-full border ${riskColors[score.riskLevel] ?? 'bg-gray-100 text-gray-700 border-gray-200'}`}>
                {score.riskLevel}
              </span>
            </div>
          ))}
        </div>
      </section>

      {/* Priority Order */}
      {data.priorityOrder.length > 0 && (
        <section>
          <h3 className="text-sm font-semibold text-gray-500 uppercase tracking-wide mb-2">
            優先順位
          </h3>
          <div className="flex gap-2 items-center">
            {data.priorityOrder.map((div, i) => (
              <div key={div} className="flex items-center gap-2">
                {i > 0 && <span className="text-gray-300 font-bold">→</span>}
                <span className="bg-brand-50 text-brand-700 text-sm font-semibold px-3 py-1 rounded-lg border border-brand-100">
                  {divisionLabels[div] ?? div}
                </span>
              </div>
            ))}
          </div>
        </section>
      )}

      {/* Impact Reasons */}
      <section>
        <h3 className="text-sm font-semibold text-gray-500 uppercase tracking-wide mb-2">
          影響要因
        </h3>
        <div className="bg-white rounded-xl border border-gray-200 p-4">
          <ul className="space-y-2">
            {data.impactReasons.map((reason, i) => (
              <li key={i} className="flex items-start gap-2 text-sm">
                <span className="mt-1 w-1.5 h-1.5 rounded-full bg-brand-500 flex-shrink-0" />
                <span className="leading-relaxed">{reason}</span>
              </li>
            ))}
          </ul>
        </div>
      </section>

      {/* KPI References */}
      {data.kpiReferences.length > 0 && (
        <section>
          <h3 className="text-sm font-semibold text-gray-500 uppercase tracking-wide mb-2">
            参照KPI
          </h3>
          <div className="bg-white rounded-xl border border-gray-200 overflow-hidden">
            <table className="w-full text-sm">
              <thead>
                <tr className="bg-gray-50 text-gray-500 text-xs uppercase">
                  <th className="text-left px-4 py-2">KPI名</th>
                  <th className="text-right px-4 py-2">値</th>
                  <th className="text-left px-4 py-2">テーブル</th>
                </tr>
              </thead>
              <tbody>
                {data.kpiReferences.map((kpi, i) => (
                  <tr key={i} className="border-t border-gray-100">
                    <td className="px-4 py-2 font-medium">{kpi.logicalNameJa || kpi.physicalName}</td>
                    <td className="px-4 py-2 text-right tabular-nums">{kpi.value ?? '—'} {kpi.unit ?? ''}</td>
                    <td className="px-4 py-2 text-gray-500 text-xs">{kpi.table ?? '—'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      )}
    </div>
  );
}
