import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { AuthService } from './auth.service';
import {
  AssistantPromptSettings,
  Campaign,
  CampaignStatus,
  CampaignSummary,
  Citation,
  DocumentContent,
  DocumentSummary,
  MetricsSummary,
  PromptPreviewResult,
  PromptUpdateResult,
  PromptVersion,
  Usage,
} from './models';

/** Cuerpo de PUT /prompt y de la parte "candidato" de POST /prompt/preview. */
export interface PromptFormValue {
  tone: string | null;
  detailLevel: string | null;
  mandatoryNotice: string | null;
  avoidWords: string[];
  extraInstructions: string | null;
}

/** Filtros del informe de métricas; los mismos para el resumen y para el CSV. */
export interface MetricsFilter {
  operators?: string[];
  monthFrom?: string;
  monthTo?: string;
  /** Id de campaña, o "none" para el histórico anterior a las campañas. */
  campaignId?: string;
}

/** Callbacks del stream de una pregunta. */
export interface AskHandlers {
  onToken: (text: string) => void;
  onCitations: (citations: Citation[]) => void;
  onUsage: (usage: Usage) => void;
  onDone: (conversationId: string) => void;
}

@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);

  // --- Chat (SSE) ---

  /**
   * Envía una pregunta y procesa el stream de Server-Sent Events. La campaña es
   * obligatoria y sin valor por defecto: es la frontera que impide responder con
   * documentación de otra campaña.
   *
   * Usamos fetch (no EventSource) porque necesitamos POST con cabecera Authorization.
   */
  async ask(
    question: string, campaignId: string, conversationId: string | null, handlers: AskHandlers
  ): Promise<void> {
    const response = await fetch('/api/v1/chat/ask', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${this.auth.token()}`,
      },
      body: JSON.stringify({ question, campaignId, conversationId }),
    });

    if (!response.ok || !response.body) {
      // El error llega como JSON (no como SSE) cuando la petición se rechaza antes de
      // empezar a responder: campaña inactiva, conversación de otra campaña, etc.
      let message = `La API respondió ${response.status}`;
      try {
        const body = await response.json();
        if (body?.message) message = body.message;
      } catch { /* el cuerpo no era JSON; se mantiene el mensaje genérico */ }
      throw new Error(message);
    }

    const reader = response.body.getReader();
    const decoder = new TextDecoder();
    let buffer = '';

    while (true) {
      const { done, value } = await reader.read();
      if (done) break;

      buffer += decoder.decode(value, { stream: true });

      // Los eventos SSE se separan por una línea en blanco.
      const events = buffer.split('\n\n');
      buffer = events.pop() ?? '';

      for (const raw of events) {
        const nameLine = raw.split('\n').find((l) => l.startsWith('event: '));
        const dataLine = raw.split('\n').find((l) => l.startsWith('data: '));
        if (!nameLine || !dataLine) continue;

        const name = nameLine.slice(7).trim();
        const data = JSON.parse(dataLine.slice(6));

        switch (name) {
          case 'token': handlers.onToken(data.text); break;
          case 'citations': handlers.onCitations(data); break;
          case 'usage': handlers.onUsage(data); break;
          case 'done': handlers.onDone(data.conversationId); break;
        }
      }
    }
  }

  // --- Campañas ---

  /** Todas las campañas, con el volumen de su corpus. Para el administrador. */
  listCampaigns() {
    return firstValueFrom(this.http.get<Campaign[]>('/api/v1/campaigns'));
  }

  /** Solo las activas: alimenta el selector del agente. */
  listActiveCampaigns() {
    return firstValueFrom(this.http.get<CampaignSummary[]>('/api/v1/campaigns/active'));
  }

  createCampaign(name: string) {
    return firstValueFrom(this.http.post<Campaign>('/api/v1/campaigns', { name }));
  }

  updateCampaign(id: string, name: string) {
    return firstValueFrom(this.http.put<Campaign>(`/api/v1/campaigns/${id}`, { name }));
  }

  /**
   * Cambia el estado de una campaña. El servidor decide si la transición es válida
   * (p. ej. no se puede cerrar una campaña activa) y responde 409 si no lo es.
   */
  setCampaignStatus(id: string, status: CampaignStatus) {
    return firstValueFrom(
      this.http.post<Campaign>(`/api/v1/campaigns/${id}/status`, { status })
    );
  }

  /** Solo permitido si la campaña está cerrada; se lleva su corpus por delante. */
  deleteCampaign(id: string) {
    return firstValueFrom(this.http.delete(`/api/v1/campaigns/${id}`));
  }

  // --- Prompt por capas ---

  /** Instrucciones vigentes; isEmpty=true significa "solo el núcleo". */
  getCampaignPrompt(campaignId: string) {
    return firstValueFrom(
      this.http.get<AssistantPromptSettings>(`/api/v1/campaigns/${campaignId}/prompt`)
    );
  }

  /** Publica unas instrucciones nuevas. Un formulario vacío restaura el comportamiento por defecto. */
  updateCampaignPrompt(campaignId: string, settings: PromptFormValue) {
    return firstValueFrom(
      this.http.put<PromptUpdateResult>(`/api/v1/campaigns/${campaignId}/prompt`, settings)
    );
  }

  /** Historial de instrucciones, más reciente primero. */
  listCampaignPromptVersions(campaignId: string) {
    return firstValueFrom(
      this.http.get<PromptVersion[]>(`/api/v1/campaigns/${campaignId}/prompt/versions`)
    );
  }

  /** Restaurar crea una entrada de historial propia; no reescribe la que restaura. */
  restoreCampaignPromptVersion(campaignId: string, versionId: string) {
    return firstValueFrom(
      this.http.post<PromptUpdateResult>(
        `/api/v1/campaigns/${campaignId}/prompt/versions/${versionId}/restore`, {}
      )
    );
  }

  /**
   * Compara, para la misma pregunta de prueba, la respuesta con lo publicado y con un
   * candidato sin guardar. No crea conversación ni telemetría.
   */
  previewCampaignPrompt(campaignId: string, question: string, candidate: PromptFormValue) {
    return firstValueFrom(
      this.http.post<PromptPreviewResult>(
        `/api/v1/campaigns/${campaignId}/prompt/preview`, { question, ...candidate }
      )
    );
  }

  // --- Feedback ---

  sendFeedback(messageId: string, rating: 'positive' | 'negative', comment?: string) {
    return firstValueFrom(
      this.http.post('/api/v1/feedback', { messageId, rating, comment: comment ?? null })
    );
  }

  // --- Conversaciones ---

  getConversation(conversationId: string) {
    return firstValueFrom(this.http.get<any>(`/api/v1/conversations/${conversationId}`));
  }

  // --- Documentos ---

  /** Documentos de una campaña. Sin ella, los de todas (vista de administración). */
  listDocuments(campaignId?: string) {
    let params = new HttpParams();
    if (campaignId) params = params.set('campaignId', campaignId);
    return firstValueFrom(this.http.get<DocumentSummary[]>('/api/v1/documents', { params }));
  }

  /**
   * Sube un documento a una campaña. Si esa campaña ya tiene un fichero con el mismo
   * nombre, la API responde 409 y hay que reintentar con `replace = true` para
   * sustituirlo.
   */
  uploadDocument(file: File, campaignId: string, options?: { title?: string; replace?: boolean }) {
    const form = new FormData();
    form.append('file', file);
    form.append('campaignId', campaignId);
    if (options?.title) form.append('title', options.title);
    if (options?.replace) form.append('replace', 'true');
    return firstValueFrom(this.http.post<DocumentSummary>('/api/v1/documents', form));
  }

  deleteDocument(id: string) {
    return firstValueFrom(this.http.delete(`/api/v1/documents/${id}`));
  }

  /** Elimina varios documentos en una sola operación. */
  deleteDocuments(documentIds: string[]) {
    return firstValueFrom(
      this.http.post<{ deleted: number; notFound: string[] }>(
        '/api/v1/documents/delete', { documentIds }
      )
    );
  }

  /** Fragmentos indexados de un documento, para consultarlo desde la interfaz. */
  getDocumentContent(id: string) {
    return firstValueFrom(this.http.get<DocumentContent>(`/api/v1/documents/${id}/content`));
  }

  /**
   * Activa o desactiva documentos. Un documento inactivo conserva lo indexado pero
   * queda fuera de las búsquedas, así que reactivarlo es inmediato y sin coste.
   */
  setDocumentsActive(documentIds: string[], isActive: boolean) {
    return firstValueFrom(
      this.http.post<DocumentSummary[]>('/api/v1/documents/active', { documentIds, isActive })
    );
  }

  // --- Métricas ---

  /** Resumen de métricas: operadores, meses y campaña son opcionales. */
  getMetrics(filter: MetricsFilter = {}) {
    return firstValueFrom(
      this.http.get<MetricsSummary>('/api/v1/metrics/summary', { params: this.metricsParams(filter) })
    );
  }

  getOperators() {
    return firstValueFrom(this.http.get<string[]>('/api/v1/metrics/operators'));
  }

  /**
   * Descarga el CSV con exactamente los mismos filtros que `getMetrics`: reutiliza la
   * misma consulta en el servidor, así que "respetar los filtros aplicados" es cierto
   * por construcción. El navegador no puede seguir un enlace con la cabecera
   * Authorization, así que se pide como blob (el interceptor sí la añade) y se
   * dispara la descarga con un enlace efímero.
   */
  async exportMetricsCsv(filter: MetricsFilter = {}): Promise<void> {
    const response = await firstValueFrom(
      this.http.get('/api/v1/metrics/export.csv', {
        params: this.metricsParams(filter),
        responseType: 'blob',
        observe: 'response',
      })
    );

    const disposition = response.headers.get('Content-Disposition') ?? '';
    const fileName = /filename="?([^";]+)"?/.exec(disposition)?.[1] ?? 'metricas.csv';

    const url = URL.createObjectURL(response.body!);
    const link = document.createElement('a');
    link.href = url;
    link.download = fileName;
    link.click();
    URL.revokeObjectURL(url);
  }

  private metricsParams(filter: MetricsFilter): HttpParams {
    let params = new HttpParams();
    for (const op of filter.operators ?? []) params = params.append('operator', op);
    if (filter.monthFrom) params = params.set('monthFrom', filter.monthFrom);
    if (filter.monthTo) params = params.set('monthTo', filter.monthTo);
    if (filter.campaignId) params = params.set('campaignId', filter.campaignId);
    return params;
  }
}
