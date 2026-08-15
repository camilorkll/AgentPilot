import { DecimalPipe } from '@angular/common';
import { Component, ElementRef, computed, inject, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/api.service';
import { CampaignSummary, ChatMessage } from '../../core/models';

const CAMPAIGN_KEY = 'agentpilot.chat.campaignId';

@Component({
  selector: 'app-chat',
  imports: [FormsModule, DecimalPipe],
  templateUrl: './chat.html',
  styleUrl: './chat.css',
})
export class Chat {
  private readonly api = inject(ApiService);
  private readonly scroller = viewChild<ElementRef<HTMLElement>>('scroller');

  question = '';
  readonly messages = signal<ChatMessage[]>([]);
  readonly sending = signal(false);
  readonly error = signal<string | null>(null);
  private conversationId: string | null = null;

  /** Mensaje cuyo motivo de «no útil» se está escribiendo, y el borrador del texto. */
  readonly commentingId = signal<string | null>(null);
  readonly commentDraft = signal('');

  /** Campañas activas: solo sobre ellas se puede preguntar. */
  readonly campaigns = signal<CampaignSummary[]>([]);
  readonly campaignsLoaded = signal(false);
  readonly campaignId = signal<string | null>(localStorage.getItem(CAMPAIGN_KEY));
  readonly selectedCampaign = computed(() =>
    this.campaigns().find((c) => c.id === this.campaignId()) ?? null
  );

  /** Sugerencias para arrancar la demo sin escribir. */
  readonly examples = [
    '¿Puedo cambiar de tarifa y tiene algún coste?',
    '¿Cuánto cuesta el Bono Viaje de 10 GB?',
    '¿Qué hago si la luz LOS del router está en rojo?',
  ];

  constructor() {
    this.loadCampaigns();
  }

  private async loadCampaigns(): Promise<void> {
    try {
      const campaigns = await this.api.listActiveCampaigns();
      this.campaigns.set(campaigns);

      // La campaña recordada puede haberse desactivado entre sesiones: si ya no está
      // entre las activas, no se puede seguir preguntando con ella.
      if (this.campaignId() && !campaigns.some((c) => c.id === this.campaignId())) {
        this.campaignId.set(null);
        localStorage.removeItem(CAMPAIGN_KEY);
      }
    } catch {
      this.error.set('No se pudieron cargar las campañas.');
    } finally {
      this.campaignsLoaded.set(true);
    }
  }

  /**
   * Cambia la campaña de trabajo. Si hay una conversación abierta, cambiar de campaña
   * exige empezar otra: el historial se reenvía al modelo en cada turno, así que
   * seguir la conversación arrastraría contenido de la campaña anterior.
   */
  selectCampaign(id: string): void {
    if (id === this.campaignId()) return;

    if (this.messages().length > 0) {
      const confirmado = confirm(
        'Cambiar de campaña empieza una conversación nueva; se perderá el historial ' +
        'de esta pantalla (queda guardado). ¿Continuar?'
      );
      if (!confirmado) return;

      this.messages.set([]);
      this.conversationId = null;
    }

    this.campaignId.set(id);
    localStorage.setItem(CAMPAIGN_KEY, id);
  }

  useExample(text: string): void {
    this.question = text;
    this.send();
  }

  async send(): Promise<void> {
    const question = this.question.trim();
    const campaignId = this.campaignId();
    if (!question || !campaignId || this.sending()) return;

    this.question = '';
    this.error.set(null);
    this.sending.set(true);

    // Pregunta del usuario + hueco para la respuesta que va a llegar en streaming.
    this.messages.update((m) => [
      ...m,
      { role: 'user', content: question },
      { role: 'assistant', content: '', streaming: true },
    ]);
    this.scrollToBottom();

    const patchLast = (patch: Partial<ChatMessage>) =>
      this.messages.update((all) =>
        all.map((m, i) => (i === all.length - 1 ? { ...m, ...patch } : m))
      );

    try {
      await this.api.ask(question, campaignId, this.conversationId, {
        onToken: (text) => {
          this.messages.update((all) =>
            all.map((m, i) => (i === all.length - 1 ? { ...m, content: m.content + text } : m))
          );
          this.scrollToBottom();
        },
        onCitations: (citations) => patchLast({ citations }),
        onUsage: (usage) => patchLast({ usage }),
        onDone: async (conversationId) => {
          this.conversationId = conversationId;
          patchLast({ streaming: false });
          await this.attachMessageId(conversationId);
        },
      });
    } catch (e: any) {
      this.error.set(e?.message ?? 'No se pudo obtener la respuesta. Revisa que la API esté disponible.');
      patchLast({ streaming: false });
    } finally {
      this.sending.set(false);
      this.scrollToBottom();
    }
  }

  /** Recupera el id del último mensaje del asistente para poder valorarlo. */
  private async attachMessageId(conversationId: string): Promise<void> {
    try {
      const conversation = await this.api.getConversation(conversationId);
      const last = [...conversation.messages].reverse().find((m: any) => m.role === 'assistant');
      if (last) {
        this.messages.update((all) =>
          all.map((m, i) => (i === all.length - 1 ? { ...m, id: last.id } : m))
        );
      }
    } catch {
      // El feedback es opcional: si falla, el chat sigue funcionando.
    }
  }

  /**
   * Publica la valoración. El servidor hace upsert: volver a valorar el mismo mensaje
   * rectifica, no añade una valoración más (contaría dos veces esa respuesta en el
   * porcentaje de respuestas útiles).
   */
  async rate(message: ChatMessage, rating: 'positive' | 'negative', comment?: string): Promise<void> {
    if (!message.id) return;
    try {
      await this.api.sendFeedback(message.id, rating, comment);
      this.messages.update((all) =>
        all.map((m) => (m.id === message.id
          ? { ...m, feedbackSent: rating, feedbackComment: comment?.trim() || undefined }
          : m))
      );
      this.cancelComment();
    } catch {
      this.error.set('No se pudo registrar la valoración.');
    }
  }

  /** Un «no útil» abre la caja de motivo en vez de enviarse directamente. */
  startNegative(message: ChatMessage): void {
    if (!message.id) return;
    this.commentingId.set(message.id);
    this.commentDraft.set('');
  }

  submitNegative(message: ChatMessage): Promise<void> {
    return this.rate(message, 'negative', this.commentDraft());
  }

  cancelComment(): void {
    this.commentingId.set(null);
    this.commentDraft.set('');
  }

  /** Vuelve a mostrar los pulgares para corregir una valoración ya emitida. */
  changeRating(message: ChatMessage): void {
    this.cancelComment();
    this.messages.update((all) =>
      all.map((m) => (m.id === message.id
        ? { ...m, feedbackSent: undefined, feedbackComment: undefined }
        : m))
    );
  }

  private scrollToBottom(): void {
    setTimeout(() => {
      const el = this.scroller()?.nativeElement;
      if (el) el.scrollTop = el.scrollHeight;
    });
  }
}
