/** Contratos de la API de AgentPilot (espejo de docs/openapi.yaml). */

export type CampaignStatus = 'inactive' | 'active' | 'closed';

/** Proyección reducida para el selector del agente: no incluye configuración. */
export interface CampaignSummary {
  id: string;
  name: string;
  activeDocumentCount: number;
}

/** Campaña completa, para el mantenimiento del administrador. */
export interface Campaign {
  id: string;
  name: string;
  status: CampaignStatus;
  documentCount: number;
  activeDocumentCount: number;
  assistantInstructions: string | null;
  closedAtUtc: string | null;
  createdAtUtc: string;
}

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
  /** Campaña a la que pertenece; el asistente solo usa el corpus de la suya. */
  campaignId: string;
  title: string;
  fileName: string;
  status: 'pending' | 'processing' | 'ready' | 'failed';
  chunkCount: number | null;
  embeddingModel: string | null;
  errorMessage: string | null;
  createdAtUtc: string;
  /** Un documento inactivo conserva sus fragmentos pero queda fuera de las búsquedas. */
  isActive: boolean;
}

/** Fragmento indexado de un documento (lo que realmente usa la búsqueda). */
export interface DocumentChunk {
  ordinal: number;
  content: string;
  charCount: number;
}

export interface DocumentContent {
  id: string;
  title: string;
  fileName: string;
  embeddingModel: string | null;
  chunks: DocumentChunk[];
}

export interface OperatorUsage {
  userName: string;
  questions: number;
  totalCostUsd: number;
  avgLatencyMs: number;
  positiveFeedbackRate: number | null;
}

export interface MetricsSummary {
  totalQuestions: number;
  positiveFeedbackRate: number | null;
  avgLatencyMs: number;
  p95LatencyMs: number;
  totalCostUsd: number;
  costByModel: Record<string, number>;
  questionsPerDay: { date: string; count: number }[];
  byOperator: OperatorUsage[];
  filteredOperators: string[];
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
