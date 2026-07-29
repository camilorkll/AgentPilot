/** Contratos de la API de AgentPilot (espejo de docs/openapi.yaml). */

export interface LoginResponse {
  accessToken: string;
  role: 'agent' | 'admin';
  expiresAtUtc: string;
}

export interface Citation {
  documentId: string;
  documentTitle: string;
  chunkId: string;
  snippet: string;
  score: number;
}

export interface Usage {
  model: string;
  promptTokens: number;
  completionTokens: number;
  estimatedCostUsd: number;
  latencyMs: number;
}

export interface DocumentSummary {
  id: string;
  title: string;
  fileName: string;
  status: 'pending' | 'processing' | 'ready' | 'failed';
  chunkCount: number | null;
  embeddingModel: string | null;
  errorMessage: string | null;
  createdAtUtc: string;
}

export interface MetricsSummary {
  totalQuestions: number;
  positiveFeedbackRate: number | null;
  avgLatencyMs: number;
  p95LatencyMs: number;
  totalCostUsd: number;
  costByModel: Record<string, number>;
  questionsPerDay: { date: string; count: number }[];
}

/** Mensaje mostrado en el chat. */
export interface ChatMessage {
  id?: string;
  role: 'user' | 'assistant';
  content: string;
  citations?: Citation[];
  usage?: Usage;
  streaming?: boolean;
  feedbackSent?: 'positive' | 'negative';
}
