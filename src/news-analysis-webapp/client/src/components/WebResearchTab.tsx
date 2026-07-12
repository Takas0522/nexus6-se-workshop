import { WebResearchResult } from '../types/api';

interface Props {
  data: WebResearchResult;
}

export default function WebResearchTab({ data }: Props) {
  return (
    <div className="space-y-5">
      {/* Summary */}
      <section>
        <h3 className="text-sm font-semibold text-gray-500 uppercase tracking-wide mb-2">サマリー</h3>
        <div className="bg-white rounded-xl border border-gray-200 p-4">
          <p className="text-sm leading-relaxed whitespace-pre-wrap">{data.summary}</p>
        </div>
      </section>

      {/* Key Factors */}
      <section>
        <h3 className="text-sm font-semibold text-gray-500 uppercase tracking-wide mb-2">
          主要ファクター
        </h3>
        <div className="grid gap-2">
          {data.key_factors.map((factor, i) => (
            <div key={i} className="flex items-start gap-3 bg-white rounded-lg border border-gray-200 p-3">
              <span className="flex-shrink-0 w-6 h-6 rounded-full bg-brand-100 text-brand-700 flex items-center justify-center text-xs font-bold">
                {i + 1}
              </span>
              <p className="text-sm leading-relaxed">{factor}</p>
            </div>
          ))}
        </div>
      </section>

      {/* Sources */}
      {data.source_urls.length > 0 && (
        <section>
          <h3 className="text-sm font-semibold text-gray-500 uppercase tracking-wide mb-2">
            ソース
          </h3>
          <div className="bg-white rounded-xl border border-gray-200 p-4 space-y-1">
            {data.source_urls.map((url, i) => (
              <a
                key={i}
                href={url}
                target="_blank"
                rel="noopener noreferrer"
                className="block text-xs text-brand-600 hover:underline truncate"
              >
                {url}
              </a>
            ))}
          </div>
        </section>
      )}
    </div>
  );
}
