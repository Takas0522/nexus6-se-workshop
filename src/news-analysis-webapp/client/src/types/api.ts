export interface NewsArticle {
  id: string;
  title: string;
  category: string;
  url: string;
  summary: string;
}

export interface WebResearchResult {
  summary: string;
  key_factors: string[];
  source_urls: string[];
}

export interface ImpactScore {
  division: 'Mobile' | 'Ecommerce' | 'Fintech';
  score: number;
  riskLevel: string;
}

export interface KpiReference {
  physicalName: string;
  logicalNameJa: string;
  value?: string;
  unit?: string;
  table?: string;
}

export interface NextAction {
  title: string;
  body: string;
}

export interface BusinessImpactResult {
  impactScores: ImpactScore[];
  impactReasons: string[];
  priorityOrder: string[];
  dataReferences: string[];
  sourceFiles: string[];
  kpiReferences: KpiReference[];
}

export interface DivisionRecommendation {
  division: 'Mobile' | 'Ecommerce' | 'Fintech';
  headline: string;
  nextActions: NextAction[];
  dataReferences: string[];
  sourceFiles: string[];
  kpiReferences: KpiReference[];
}

export interface AnalysisResponse {
  webResearch: WebResearchResult | null;
  businessImpact: BusinessImpactResult | null;
  recommendations: DivisionRecommendation[];
  startedAt: string;
  completedAt: string | null;
}
