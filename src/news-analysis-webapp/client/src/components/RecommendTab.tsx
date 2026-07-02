import { DivisionRecommendation } from '../types/api';

interface Props {
  data: DivisionRecommendation[];
}

const divisionLabels: Record<string, string> = {
  Mobile: 'モバイル通信',
  Ecommerce: 'Eコマース',
  Fintech: 'フィンテック',
};

const divisionIcons: Record<string, string> = {
  Mobile: '📱',
  Ecommerce: '🛒',
  Fintech: '💳',
};

const divisionColors: Record<string, string> = {
  Mobile: 'border-l-blue-500',
  Ecommerce: 'border-l-purple-500',
  Fintech: 'border-l-emerald-500',
};

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
      {data.map((rec) => (
        <div
          key={rec.division}
          className={`bg-white rounded-xl border border-gray-200 border-l-4 ${divisionColors[rec.division] ?? 'border-l-gray-400'} overflow-hidden`}
        >
          {/* Division Header */}
          <div className="px-5 py-3 bg-gray-50 border-b border-gray-100 flex items-center gap-2">
            <span className="text-lg">{divisionIcons[rec.division] ?? '📊'}</span>
            <h3 className="font-bold text-sm">{divisionLabels[rec.division] ?? rec.division}</h3>
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
                  {rec.kpiReferences.map((kpi, i) => (
                    <span key={i} className="inline-flex items-center gap-1 text-xs bg-brand-50 text-brand-700 px-2 py-1 rounded-md border border-brand-100">
                      {kpi.logicalNameJa || kpi.physicalName}
                      {kpi.value && <span className="font-semibold">{kpi.value}{kpi.unit ?? ''}</span>}
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
