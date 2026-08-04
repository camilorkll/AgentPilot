import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/api.service';
import { Campaign, DocumentContent, DocumentSummary } from '../../core/models';

const CAMPAIGN_KEY = 'agentpilot.documents.campaignId';

@Component({
  selector: 'app-documents',
  imports: [DatePipe, FormsModule],
  templateUrl: './documents.html',
  styleUrl: './documents.css',
})
export class Documents {
  private readonly api = inject(ApiService);

  readonly documents = signal<DocumentSummary[]>([]);
  readonly error = signal<string | null>(null);

  /** Campaña sobre la que se trabaja: filtra la tabla y es el destino de la subida. */
  readonly campaigns = signal<Campaign[]>([]);
  readonly campaignsLoaded = signal(false);
  readonly campaignId = signal<string | null>(localStorage.getItem(CAMPAIGN_KEY));
  readonly selectedCampaign = computed(() =>
    this.campaigns().find((c) => c.id === this.campaignId()) ?? null
  );
  /** Una campaña cerrada es de solo lectura: se puede consultar, no subir ni borrar. */
  readonly canEditDocumentation = computed(() => this.selectedCampaign()?.status !== 'closed');

  /** Progreso de la subida múltiple: "subiendo 3 de 12". */
  readonly uploadTotal = signal(0);
  readonly uploadDone = signal(0);
  readonly uploading = computed(() => this.uploadTotal() > 0);

  /** Selección para el borrado múltiple. */
  readonly selected = signal<Set<string>>(new Set());
  readonly selectedCount = computed(() => this.selected().size);
  readonly allSelected = computed(() =>
    this.documents().length > 0 && this.selected().size === this.documents().length
  );

  /** Documento que se está consultando (visor de fragmentos). */
  readonly viewing = signal<DocumentContent | null>(null);
  readonly loadingContent = signal(false);

  constructor() {
    this.loadCampaigns();
  }

  private async loadCampaigns(): Promise<void> {
    try {
      const campaigns = await this.api.listCampaigns();
      this.campaigns.set(campaigns);

      // La campaña recordada puede haber sido eliminada entre sesiones.
      if (this.campaignId() && !campaigns.some((c) => c.id === this.campaignId())) {
        this.campaignId.set(null);
        localStorage.removeItem(CAMPAIGN_KEY);
      }
    } catch {
      this.error.set('No se pudieron cargar las campañas.');
    } finally {
      this.campaignsLoaded.set(true);
      await this.refresh();
    }
  }

  selectCampaign(id: string): void {
    this.campaignId.set(id);
    localStorage.setItem(CAMPAIGN_KEY, id);
    this.selected.set(new Set());
    this.refresh();
  }

  async refresh(): Promise<void> {
    const campaignId = this.campaignId();
    if (!campaignId) {
      this.documents.set([]);
      return;
    }
    try {
      const documents = await this.api.listDocuments(campaignId);
      this.documents.set(documents);
      // Descarta de la selección lo que ya no exista.
      const ids = new Set(documents.map((d) => d.id));
      this.selected.update((s) => new Set([...s].filter((id) => ids.has(id))));
    } catch {
      this.error.set('No se pudo cargar el listado de documentos.');
    }
  }

  // --- Subida múltiple ---

  /**
   * Sube los ficheros elegidos de uno en uno: la API los acepta al momento y el worker
   * los vectoriza secuencialmente, así que el listado va reflejando el progreso.
   */
  async onFilesSelected(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const files = Array.from(input.files ?? []);
    input.value = '';
    const campaignId = this.campaignId();
    if (files.length === 0 || !campaignId) return;

    this.error.set(null);
    this.uploadTotal.set(files.length);
    this.uploadDone.set(0);

    const duplicates: File[] = [];
    const failed: string[] = [];
    // El motivo lo da la API (formato no admitido, campaña cerrada…). Mostrar uno
    // inventado manda a buscar el problema donde no está.
    const reasons = new Set<string>();

    for (const file of files) {
      try {
        await this.api.uploadDocument(file, campaignId);
      } catch (e: any) {
        if (e?.status === 409 && e?.error?.code === 'duplicate_document') duplicates.push(file);
        else {
          failed.push(file.name);
          reasons.add(e?.error?.message ?? 'Error inesperado al subir el fichero.');
        }
      } finally {
        this.uploadDone.update((n) => n + 1);
      }
    }

    // Los duplicados se preguntan una sola vez, no fichero a fichero.
    if (duplicates.length > 0) {
      const names = duplicates.map((f) => `• ${f.name}`).join('\n');
      const replace = confirm(
        `${duplicates.length} documento(s) ya están en «${this.selectedCampaign()?.name}»:\n\n${names}\n\n` +
        `¿Quieres reemplazarlos? Se eliminarán las versiones anteriores y se volverán a indexar.`
      );
      if (replace) {
        this.uploadTotal.set(duplicates.length);
        this.uploadDone.set(0);
        for (const file of duplicates) {
          try {
            await this.api.uploadDocument(file, campaignId, { replace: true });
          } catch (e: any) {
            failed.push(file.name);
            reasons.add(e?.error?.message ?? 'Error inesperado al subir el fichero.');
          } finally {
            this.uploadDone.update((n) => n + 1);
          }
        }
      }
    }

    if (failed.length > 0)
      this.error.set(`No se pudieron subir: ${failed.join(', ')}. ${[...reasons].join(' ')}`);

    this.uploadTotal.set(0);
    this.uploadDone.set(0);
    await this.trackIngestion();
  }

  /** Refresca mientras queden documentos en cola o procesándose. */
  private async trackIngestion(): Promise<void> {
    for (let i = 0; i < 30; i++) {
      await this.refresh();
      const pending = this.documents().some(
        (d) => d.status === 'pending' || d.status === 'processing'
      );
      if (!pending) return;
      await new Promise((r) => setTimeout(r, 2000));
    }
    // El recuento de la campaña (nº de documentos activos) puede haber cambiado.
    await this.loadCampaigns();
  }

  // --- Selección y borrado múltiple ---

  isSelected(id: string): boolean {
    return this.selected().has(id);
  }

  toggle(id: string): void {
    this.selected.update((current) => {
      const next = new Set(current);
      next.has(id) ? next.delete(id) : next.add(id);
      return next;
    });
  }

  toggleAll(): void {
    this.selected.update((current) =>
      current.size === this.documents().length
        ? new Set()
        : new Set(this.documents().map((d) => d.id))
    );
  }

  // --- Activación / desactivación ---

  /** Alterna la disponibilidad de un documento en la base de conocimiento. */
  async toggleActive(document: DocumentSummary): Promise<void> {
    await this.setActive([document.id], !document.isActive);
  }

  async setSelectedActive(isActive: boolean): Promise<void> {
    const ids = [...this.selected()];
    if (ids.length === 0) return;
    await this.setActive(ids, isActive);
    this.selected.set(new Set());
  }

  private async setActive(ids: string[], isActive: boolean): Promise<void> {
    try {
      await this.api.setDocumentsActive(ids, isActive);
      await this.refresh();
    } catch (e: any) {
      this.error.set(
        e?.error?.message ??
        (isActive
          ? 'No se pudieron activar los documentos.'
          : 'No se pudieron desactivar los documentos.')
      );
    }
  }

  async deleteSelected(): Promise<void> {
    const ids = [...this.selected()];
    if (ids.length === 0) return;

    const names = this.documents()
      .filter((d) => ids.includes(d.id))
      .map((d) => `• ${d.title}`)
      .join('\n');

    const confirmed = confirm(
      `¿Seguro que quieres eliminar ${ids.length} documento(s) de la base de conocimiento?\n\n` +
      `${names}\n\nSe borrarán también sus fragmentos indexados. Esta acción no se puede deshacer.`
    );
    if (!confirmed) return;

    try {
      await this.api.deleteDocuments(ids);
      this.selected.set(new Set());
      await this.refresh();
    } catch (e: any) {
      this.error.set(e?.error?.message ?? 'No se pudieron eliminar los documentos seleccionados.');
    }
  }

  // --- Visor de contenido ---

  async view(document: DocumentSummary): Promise<void> {
    this.loadingContent.set(true);
    try {
      this.viewing.set(await this.api.getDocumentContent(document.id));
    } catch {
      this.error.set('No se pudo cargar el contenido del documento.');
    } finally {
      this.loadingContent.set(false);
    }
  }

  closeViewer(): void {
    this.viewing.set(null);
  }
}
