import { DivisionRecommendation } from '../types/api';
import { formatKpiPairs } from '../utils/kpiFormat';

interface Props {
  data: DivisionRecommendation[];
}

// Dynamic color/icon assignment based on index
const colorPalette = ['border-l-blue-500', 'border-l-purple-500', 'border-l-emerald-500', 'border-l-amber-500', 'border-l-rose-500', 'border-l-cyan-500'];
const iconPalette = ['📊', '💡', '🎯', '⚡', '🔍', '📈'];

export default function RecommendTab({ data }: Props) {
  if (data.length === 0) {
    return (
      <div className="flex items-center justify-center h-40 text-gray-400 text-sm">
        レコメンド結果がありません
      </div>
    );
  }

  return (
    <div className="space-y-5">
      {data.map((rec, idx) => (
        <div
          key={rec.division}
          className={`bg-white rounded-xl border border-gray-200 border-l-4 ${colorPalette[idx % colorPalette.length]} overflow-hidden`}
        >
          {/* Division Header */}
          <div className="px-5 py-3 bg-gray-50 border-b border-gray-100 flex items-center gap-2">
            <span className="text-lg">{iconPalette[idx % iconPalette.length]}</span>
            <h3 className="font-bold text-sm">{rec.division}</h3>
          </div>

          <div className="p-5 space-y-4">
            {/* Headline */}
            <div>
              <p className="text-sm font-semibold text-gray-800 leading-relaxed">{rec.headline}</p>
            </div>

            {/* Next Actions */}
            {rec.nextActions.length > 0 && (
              <div>
                <h4 className="text-xs font-semibold text-gray-500 uppercase tracking-wide mb-2">
                  推奨アクション
                </h4>
                <div className="space-y-2">
                  {rec.nextActions.map((action, i) => (
                    <div key={i} className="bg-gray-50 rounded-lg p-3">
                      <p className="text-sm font-medium text-gray-800">{action.title}</p>
                      {action.body && (
                        <p className="text-xs text-gray-500 mt-1 leading-relaxed">{action.body}</p>
                      )}
                    </div>
                  ))}
                </div>
              </div>
            )}

            {/* KPIs */}
            {rec.kpiReferences.length > 0 && (
              <div>
                <h4 className="text-xs font-semibold text-gray-500 uppercase tracking-wide mb-2">
                  関連KPI
                </h4>
                <div className="flex flex-wrap gap-2">
                  {formatKpiPairs(rec.kpiReferences).map((kpi, i) => (
                    <span key={i} className="inline-flex items-center gap-1 text-xs bg-brand-50 text-brand-700 px-2 py-1 rounded-md border border-brand-100">
                      {kpi.label}
                      {kpi.formattedValue && <span className="font-semibold">{kpi.formattedValue}</span>}
                    </span>
                  ))}
                </div>
              </div>
            )}
          </div>
        </div>
      ))}
    </div>
  );
}
