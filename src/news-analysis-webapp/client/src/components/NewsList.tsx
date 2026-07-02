import { NewsArticle } from '../types/api';

interface Props {
  articles: NewsArticle[];
  selectedId: string | null;
  onSelect: (article: NewsArticle) => void;
}

const categoryColors: Record<string, string> = {
  '為替': 'bg-blue-100 text-blue-800',
  '競合統合': 'bg-purple-100 text-purple-800',
  '日銀利上げ': 'bg-amber-100 text-amber-800',
};

export default function NewsList({ articles, selectedId, onSelect }: Props) {
  return (
    <div className="flex flex-col gap-3">
      <h2 className="text-lg font-bold text-gray-800 flex items-center gap-2">
        <svg className="w-5 h-5 text-accent" fill="none" viewBox="0 0 24 24" stroke="currentColor">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 20H5a2 2 0 01-2-2V6a2 2 0 012-2h10a2 2 0 012 2v1m2 13a2 2 0 01-2-2V7m2 13a2 2 0 002-2V9a2 2 0 00-2-2h-2m-4-3H9M7 16h6M7 8h6v4H7V8z" />
        </svg>
        NEXUS経済ニュース
      </h2>
      {articles.map((article) => (
        <button
          key={article.id}
          onClick={() => onSelect(article)}
          className={`text-left rounded-xl border p-4 transition-all duration-200 hover:shadow-md ${
            selectedId === article.id
              ? 'border-brand-500 bg-brand-50 shadow-sm ring-2 ring-brand-500/20'
              : 'border-gray-200 bg-white hover:border-gray-300'
          }`}
        >
          <span className={`inline-block text-xs font-semibold px-2 py-0.5 rounded-full mb-2 ${categoryColors[article.category] ?? 'bg-gray-100 text-gray-700'}`}>
            {article.category}
          </span>
          <h3 className="font-bold text-sm leading-snug mb-1">{article.title}</h3>
          <p className="text-xs text-gray-500 leading-relaxed">{article.summary}</p>
        </button>
      ))}
    </div>
  );
}
