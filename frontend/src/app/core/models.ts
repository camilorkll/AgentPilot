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
  closedAtUtc: string | null;
  createdAtUtc: string;
  /** Cuántas entradas conserva como máximo el historial de instrucciones (5 por defecto). */
  maxPromptVersions: number;
}

/**
 * Instrucciones propias de una campaña para el asistente: negocio (tono, avisos,
 * vocabulario), nunca reglas del sistema — esas viven en el núcleo del prompt, en
 * código, y se gestionan aparte de la campaña (ver /campaigns/{id}/prompt).
 */
export interface AssistantPromptSettings {
  tone: 'cercano' | 'neutro' | 'formal' | null;
  detailLevel: 'breve' | 'normal' | 'detallado' | null;
  mandatoryNotice: string | null;
  avoidWords: string[];
  extraInstructions: string | null;
  /** True si ningún campo está informado: el asistente responde solo con el núcleo. */
  isEmpty: boolean;
}

/** Respuesta de publicar (o restaurar) unas instrucciones de campaña. */
export interface PromptUpdateResult {
  prompt: AssistantPromptSettings;
  /** Avisos de lint no bloqueantes: se publica igualmente, el núcleo se reafirma después. */
  warnings: string[];
  versionId: string;
  createdAtUtc: string;
}

/** Una entrada del historial de instrucciones de una campaña. */
export interface PromptVersion {
  id: string;
  prompt: AssistantPromptSettings;
  publishedBy: string;
  createdAtUtc: string;
}

/** Comparación de una pregunta de prueba con lo publicado y con un candidato sin guardar. */
export interface PromptPreviewResult {
  currentAnswer: string;
  candidateAnswer: string;
  citations: Citation[];
  warnings: string[];
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

/** Actividad de un operador en un día concreto, en hora de Europe/Madrid. */
export interface DailyOperatorUsage {
  date: string;
  userName: string;
  questions: number;
  costUsd: number;
  avgLatencyMs: number;
  positiveFeedbackRate: number | null;
}

/**
 * Total de un mes. Con `userName` es el total de ESE operador (vista Agente → Días);
 * con `userName` a null es el total de todos los operadores del filtro (vista
 * Día → Agentes). No es la suma de los días: la latencia media y el % de útiles no
 * son aditivos, así que el servidor los calcula aparte.
 */
export interface MonthlyTotal {
  month: string;
  userName: string | null;
  questions: number;
  costUsd: number;
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
  /** Coste por nombre de campaña; el histórico sin campaña aparece con su propia clave. */
  costByCampaign: Record<string, number>;
  questionsPerDay: { date: string; count: number }[];
  byOperator: OperatorUsage[];
  /** Matriz (día, operador): una sola consulta, pivotada de dos formas en pantalla. */
  dailyByOperator: DailyOperatorUsage[];
  monthlyTotals: MonthlyTotal[];
  filteredOperators: string[];
  /** Mes inicial/final YA resueltos por el servidor (incluidos los valores por defecto). */
  monthFrom: string | null;
  monthTo: string | null;
  /** Campaña aplicada al informe; null si no se filtró por campaña. */
  campaignId: string | null;
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
  /** Motivo anotado en un «no útil»; opcional. */
  feedbackComment?: string;
}

/** Valoración vigente de una respuesta, tal como la devuelve GET /conversations/{id}. */
export interface MessageFeedback {
  rating: 'positive' | 'negative';
  comment: string | null;
  createdBy: string | null;
  createdAtUtc: string;
}

/**
 * Una respuesta valorada con su contexto mínimo (GET /feedback), para la pantalla de
 * revisión. No trae la conversación entera: esa se pide aparte y solo si hace falta.
 */
export interface RatedAnswer {
  messageId: string;
  conversationId: string;
  campaignId: string | null;
  campaignName: string | null;
  question: string | null;
  answer: string;
  rating: 'positive' | 'negative';
  comment: string | null;
  ratedBy: string | null;
  ratedAtUtc: string;
}
