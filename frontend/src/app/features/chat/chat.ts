import { DecimalPipe } from '@angular/common';
import { Component, ElementRef, inject, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/api.service';
import { ChatMessage } from '../../core/models';

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

  /** Sugerencias para arrancar la demo sin escribir. */
  readonly examples = [
    '¿Puedo cambiar de tarifa y tiene algún coste?',
    '¿Cuánto cuesta el Bono Viaje de 10 GB?',
    '¿Qué hago si la luz LOS del router está en rojo?',
  ];

  useExample(text: string): void {
    this.question = text;
    this.send();
  }

  async send(): Promise<void> {
    const question = this.question.trim();
    if (!question || this.sending()) return;

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
      await this.api.ask(question, this.conversationId, {
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
    } catch (e) {
      this.error.set('No se pudo obtener la respuesta. Revisa que la API esté disponible.');
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

  async rate(message: ChatMessage, rating: 'positive' | 'negative'): Promise<void> {
    if (!message.id || message.feedbackSent) return;
    try {
      await this.api.sendFeedback(message.id, rating);
      this.messages.update((all) =>
        all.map((m) => (m.id === message.id ? { ...m, feedbackSent: rating } : m))
      );
    } catch {
      this.error.set('No se pudo registrar la valoración.');
    }
  }

  private scrollToBottom(): void {
    setTimeout(() => {
      const el = this.scroller()?.nativeElement;
      if (el) el.scrollTop = el.scrollHeight;
    });
  }
}
