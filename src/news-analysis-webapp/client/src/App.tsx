import { useState, useEffect, useCallback } from 'react';
import WebResearchTab from './components/WebResearchTab';
import BusinessImpactTab from './components/BusinessImpactTab';
import RecommendTab from './components/RecommendTab';
import type { NewsArticle, AnalysisResponse } from './types/api';

const NEWS_PORTAL_BASE = '/news-portal/index.html';

type TabId = 'webResearch' | 'businessImpact' | 'recommend';

const tabs: { id: TabId; label: string; icon: string }[] = [
  { id: 'webResearch', label: 'Web検索', icon: '🔍' },
  { id: 'businessImpact', label: 'ビジネスインパクト', icon: '📊' },
  { id: 'recommend', label: 'レコメンド', icon: '💡' },
];

const categoryColors: Record<string, string> = {
  '為替': 'bg-blue-500',
  '競合統合': 'bg-purple-500',
  '日銀利上げ': 'bg-amber-500',
};

export default function App() {
  const [articles, setArticles] = useState<NewsArticle[]>([]);
  const [selectedArticle, setSelectedArticle] = useState<NewsArticle | null>(null);
  const [iframeUrl, setIframeUrl] = useState(NEWS_PORTAL_BASE);
  const [activeTab, setActiveTab] = useState<TabId>('webResearch');
  const [analysis, setAnalysis] = useState<AnalysisResponse | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    fetch('/api/news')
      .then((res) => res.json())
      .then((data: NewsArticle[]) => setArticles(data))
      .catch(() => setError('ニュース一覧の取得に失敗しました'));
  }, []);

  const runAnalysis = useCallback(async (article: NewsArticle) => {
    setSelectedArticle(article);
    setIframeUrl(article.url);
    setAnalysis(null);
    setError(null);
    setLoading(true);
    setActiveTab('webResearch');

    try {
      const res = await fetch('/api/analysis/run', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ newsText: article.summary, searchHints: [article.category] }),
      });
      if (!res.ok) throw new Error(`Analysis failed: ${res.status}`);
      const data: AnalysisResponse = await res.json();
      setAnalysis(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : '分析の実行に失敗しました');
    } finally {
      setLoading(false);
    }
  }, []);

  return (
    <div className="h-screen flex flex-col overflow-hidden">
      {/* Header */}
      <header className="bg-gradient-to-r from-slate-900 via-brand-800 to-brand-700 text-white px-6 py-3 shadow-lg flex-shrink-0">
        <div className="max-w-[1920px] mx-auto flex items-center justify-between">
          <div className="flex items-center gap-3">
            <div className="w-8 h-8 bg-white/20 rounded-lg flex items-center justify-center text-lg">🤖</div>
            <div>
              <h1 className="text-lg font-bold tracking-tight">News Analysis Dashboard</h1>
              <p className="text-xs text-white/70">Azure AI Foundry Hosted Agent</p>
            </div>
          </div>
          <div className="flex items-center gap-2 text-xs text-white/60">
            <span className="w-2 h-2 bg-green-400 rounded-full animate-pulse" />
            Agent Online
          </div>
        </div>
      </header>

      {/* Main content — two panes */}
      <div className="flex-1 flex min-h-0">
        {/* ════════ Left pane — News Portal (iframe) ════════ */}
        <aside className="w-[55%] flex-shrink-0 flex flex-col border-r border-gray-200 bg-white">
          {/* Article selector bar */}
          <div className="flex items-center gap-2 px-3 py-2 bg-gray-50 border-b border-gray-200 flex-shrink-0">
            <span className="text-xs font-semibold text-gray-400 uppercase tracking-wider mr-1">分析対象:</span>
            {articles.map((article) => (
              <button
                key={article.id}
                onClick={() => runAnalysis(article)}
                className={`flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-medium transition-all border ${
                  selectedArticle?.id === article.id
                    ? 'bg-brand-600 text-white border-brand-600 shadow-sm'
                    : 'bg-white text-gray-600 border-gray-200 hover:border-gray-300 hover:bg-gray-50'
                }`}
              >
                <span className={`w-2 h-2 rounded-full flex-shrink-0 ${categoryColors[article.category] ?? 'bg-gray-400'}`} />
                {article.category}
              </button>
            ))}
            <button
              onClick={() => { setIframeUrl(NEWS_PORTAL_BASE); }}
              className="ml-auto text-xs text-gray-400 hover:text-gray-600 px-2 py-1"
              title="トップページに戻る"
            >
              🏠 トップ
            </button>
          </div>

          {/* iframe */}
          <iframe
            src={iframeUrl}
            className="flex-1 w-full border-0"
            title="NEXUS経済ニュース"
            sandbox="allow-same-origin allow-scripts allow-popups"
          />
        </aside>

        {/* ════════ Right pane — Agent results ════════ */}
        <main className="flex-1 min-w-0 flex flex-col bg-gray-50">
          {!selectedArticle && !loading && (
            <div className="flex-1 flex items-center justify-center">
              <div className="text-center">
                <div className="text-5xl mb-4">📰</div>
                <p className="text-gray-400 text-sm">左のニュースカテゴリを選択して分析を開始</p>
              </div>
            </div>
          )}

          {selectedArticle && (
            <>
              {/* Selected article banner */}
              <div className="bg-white border-b border-gray-200 px-5 py-3 flex-shrink-0">
                <p className="text-[10px] text-gray-400 uppercase tracking-wider mb-0.5">分析対象ニュース</p>
                <h2 className="font-bold text-sm text-gray-800">{selectedArticle.title}</h2>
              </div>

              {/* Tabs */}
              <div className="flex gap-1 mx-4 mt-4 mb-3 bg-gray-100 rounded-xl p-1 flex-shrink-0">
                {tabs.map((tab) => (
                  <button
                    key={tab.id}
                    onClick={() => setActiveTab(tab.id)}
                    className={`flex-1 flex items-center justify-center gap-1.5 py-2 px-3 rounded-lg text-sm font-medium transition-all ${
                      activeTab === tab.id
                        ? 'bg-white text-gray-900 shadow-sm'
                        : 'text-gray-500 hover:text-gray-700'
                    }`}
                  >
                    <span>{tab.icon}</span>
                    {tab.label}
                  </button>
                ))}
              </div>

              {/* Tab content */}
              <div className="flex-1 overflow-y-auto px-4 pb-4">
                {loading && (
                  <div className="flex items-center justify-center h-40">
                    <div className="flex items-center gap-3">
                      <div className="w-5 h-5 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
                      <span className="text-sm text-gray-500">エージェントが分析中...</span>
                    </div>
                  </div>
                )}

                {error && (
                  <div className="bg-red-50 border border-red-200 rounded-xl p-4 text-sm text-red-700">
                    {error}
                  </div>
                )}

                {!loading && !error && analysis && (
                  <>
                    {activeTab === 'webResearch' && analysis.webResearch && (
                      <WebResearchTab data={analysis.webResearch} />
                    )}
                    {activeTab === 'webResearch' && !analysis.webResearch && (
                      <EmptyState message="Web検索結果がありません" />
                    )}
                    {activeTab === 'businessImpact' && analysis.businessImpact && (
                      <BusinessImpactTab data={analysis.businessImpact} />
                    )}
                    {activeTab === 'businessImpact' && !analysis.businessImpact && (
                      <EmptyState message="ビジネスインパクト結果がありません" />
                    )}
                    {activeTab === 'recommend' && (
                      <RecommendTab data={analysis.recommendations} />
                    )}
                  </>
                )}
              </div>
            </>
          )}
        </main>
      </div>
    </div>
  );
}

function EmptyState({ message }: { message: string }) {
  return (
    <div className="flex items-center justify-center h-40 text-gray-400 text-sm">
      {message}
    </div>
  );
}
