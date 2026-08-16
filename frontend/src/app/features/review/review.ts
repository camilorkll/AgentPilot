import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/api.service';
import { Campaign, ChatMessage, RatedAnswer } from '../../core/models';

/** Turnos de una conversación ya abierta, para mostrarla bajo la respuesta valorada. */
interface OpenConversation {
  id: string;
  messages: ChatMessage[];
}

@Component({
  selector: 'app-review',
  imports: [DatePipe, FormsModule],
  templateUrl: './review.html',
  styleUrl: './review.css',
})
export class Review {
  private readonly api = inject(ApiService);

  readonly answers = signal<RatedAnswer[]>([]);
  readonly campaigns = signal<Campaign[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);

  // Por defecto se muestran solo las negativas: son las que hay que revisar. Las
  // positivas están para poder contrastar, no para leerlas de una en una.
  rating: '' | 'positive' | 'negative' = 'negative';
  campaignId = '';

  /** Conversación desplegada, si el revisor pidió ver uno de los hilos completos. */
  readonly openConversation = signal<OpenConversation | null>(null);
  readonly openingId = signal<string | null>(null);

  constructor() {
    this.loadCampaigns();
    this.refresh();
  }

  private async loadCampaigns(): Promise<void> {
    try {
      this.campaigns.set(await this.api.listCampaigns());
    } catch {
      // El filtro por campaña es una comodidad: si falla, el listado sigue sirviendo.
    }
  }

  async refresh(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    this.openConversation.set(null);
    try {
      this.answers.set(await this.api.listRatedAnswers({
        rating: this.rating || undefined,
        campaignId: this.campaignId || undefined,
      }));
    } catch (e: any) {
      this.error.set(e?.error?.message ?? 'No se pudieron cargar las respuestas valoradas.');
    } finally {
      this.loading.set(false);
    }
  }

  /**
   * Abre el hilo completo de una respuesta. Es una acción explícita, no algo que el
   * listado traiga de serie: la conversación entera puede contener datos del cliente
   * que no hacen falta para juzgar por qué falló esta respuesta (ver SECURITY.md).
   */
  async openThread(answer: RatedAnswer): Promise<void> {
    if (this.openConversation()?.id === answer.conversationId) {
      this.openConversation.set(null);
      return;
    }

    this.openingId.set(answer.conversationId);
    this.error.set(null);
    try {
      const conversation = await this.api.getConversation(answer.conversationId);
      this.openConversation.set({ id: answer.conversationId, messages: conversation.messages });
    } catch (e: any) {
      this.error.set(e?.error?.message ?? 'No se pudo abrir la conversación.');
    } finally {
      this.openingId.set(null);
    }
  }

  isOpen(answer: RatedAnswer): boolean {
    return this.openConversation()?.id === answer.conversationId;
  }

  /** Marca el turno concreto que se valoró dentro del hilo, para no perderlo de vista. */
  isRatedMessage(answer: RatedAnswer, message: ChatMessage): boolean {
    return message.id === answer.messageId;
  }
}
