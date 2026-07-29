import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { AuthService } from './auth.service';
import { Citation, DocumentSummary, MetricsSummary, Usage } from './models';

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
   * Envía una pregunta y procesa el stream de Server-Sent Events.
   * Usamos fetch (no EventSource) porque necesitamos POST con cabecera Authorization.
   */
  async ask(question: string, conversationId: string | null, handlers: AskHandlers): Promise<void> {
    const response = await fetch('/api/v1/chat/ask', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${this.auth.token()}`,
      },
      body: JSON.stringify({ question, conversationId }),
    });

    if (!response.ok || !response.body) {
      throw new Error(`La API respondió ${response.status}`);
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

  listDocuments() {
    return firstValueFrom(this.http.get<DocumentSummary[]>('/api/v1/documents'));
  }

  /**
   * Sube un documento. Si ya existe uno con el mismo nombre, la API responde 409 y
   * hay que reintentar con `replace = true` para sustituirlo.
   */
  uploadDocument(file: File, options?: { title?: string; replace?: boolean }) {
    const form = new FormData();
    form.append('file', file);
    if (options?.title) form.append('title', options.title);
    if (options?.replace) form.append('replace', 'true');
    return firstValueFrom(this.http.post<DocumentSummary>('/api/v1/documents', form));
  }

  deleteDocument(id: string) {
    return firstValueFrom(this.http.delete(`/api/v1/documents/${id}`));
  }

  // --- Métricas ---

  getMetrics() {
    return firstValueFrom(this.http.get<MetricsSummary>('/api/v1/metrics/summary'));
  }
}
