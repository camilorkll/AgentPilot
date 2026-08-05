import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService, PromptFormValue } from '../../core/api.service';
import { AssistantPromptSettings, Campaign, CampaignStatus, PromptPreviewResult, PromptVersion } from '../../core/models';

/** Estados desde los que tiene sentido cada acción, para no mostrar botones que fallarían. */
const ACCIONES_POR_ESTADO: Record<CampaignStatus, { label: string; target: CampaignStatus }[]> = {
  active: [{ label: 'Desactivar', target: 'inactive' }],
  inactive: [
    { label: 'Activar', target: 'active' },
    { label: 'Cerrar', target: 'closed' },
  ],
  closed: [{ label: 'Reabrir', target: 'inactive' }],
};

const VACÍO: AssistantPromptSettings = {
  tone: null, detailLevel: null, mandatoryNotice: null, avoidWords: [], extraInstructions: null, isEmpty: true,
};

@Component({
  selector: 'app-campaigns',
  imports: [DatePipe, FormsModule],
  templateUrl: './campaigns.html',
  styleUrl: './campaigns.css',
})
export class Campaigns {
  private readonly api = inject(ApiService);

  readonly campaigns = signal<Campaign[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);

  // --- Alta ---
  newName = '';
  readonly creating = signal(false);

  // --- Edición en línea (una fila a la vez): solo el nombre, las instrucciones
  //     del asistente se gestionan aparte en el panel de prompt ---
  readonly editingId = signal<string | null>(null);
  draftName = '';
  readonly savingEdit = signal(false);

  // --- Borrado con confirmación reforzada ---
  readonly deleteTarget = signal<Campaign | null>(null);
  readonly deleteCounts = signal<{ documents: number; fragments: number } | null>(null);
  // Signal, no propiedad plana: canConfirmDelete es un computed() y solo se
  // reevalúa cuando cambia un signal que lee, nunca por un campo simple con ngModel.
  readonly deleteConfirmText = signal('');
  readonly deleting = signal(false);
  readonly canConfirmDelete = computed(() =>
    this.deleteTarget() !== null && this.deleteConfirmText().trim() === this.deleteTarget()!.name
  );

  // --- Panel de prompt: formulario, historial y preview ---
  readonly promptTarget = signal<Campaign | null>(null);
  readonly promptLoading = signal(false);
  readonly promptSaving = signal(false);
  readonly promptError = signal<string | null>(null);
  readonly promptVersions = signal<PromptVersion[]>([]);
  readonly promptWarnings = signal<string[]>([]);
  readonly promptEditable = computed(() => this.promptTarget()?.status !== 'closed');

  tone: '' | 'cercano' | 'neutro' | 'formal' = '';
  detailLevel: '' | 'breve' | 'normal' | 'detallado' = '';
  mandatoryNotice = '';
  avoidWordsText = '';
  extraInstructions = '';

  previewQuestion = '';
  readonly previewLoading = signal(false);
  readonly previewError = signal<string | null>(null);
  readonly previewResult = signal<PromptPreviewResult | null>(null);

  constructor() {
    this.refresh();
  }

  async refresh(): Promise<void> {
    this.loading.set(true);
    try {
      this.campaigns.set(await this.api.listCampaigns());
    } catch {
      this.error.set('No se pudieron cargar las campañas.');
    } finally {
      this.loading.set(false);
    }
  }

  actionsFor(campaign: Campaign) {
    return ACCIONES_POR_ESTADO[campaign.status];
  }

  // --- Alta ---

  async create(): Promise<void> {
    const name = this.newName.trim();
    if (!name || this.creating()) return;

    this.creating.set(true);
    this.error.set(null);
    try {
      const created = await this.api.createCampaign(name);
      this.campaigns.update((all) => [...all, created].sort((a, b) => a.name.localeCompare(b.name)));
      this.newName = '';
    } catch (e: any) {
      this.error.set(e?.error?.message ?? 'No se pudo crear la campaña.');
    } finally {
      this.creating.set(false);
    }
  }

  // --- Edición en línea (nombre) ---

  startEdit(campaign: Campaign): void {
    this.editingId.set(campaign.id);
    this.draftName = campaign.name;
  }

  cancelEdit(): void {
    this.editingId.set(null);
  }

  async saveEdit(campaign: Campaign): Promise<void> {
    const name = this.draftName.trim();
    if (!name || this.savingEdit()) return;

    this.savingEdit.set(true);
    this.error.set(null);
    try {
      const updated = await this.api.updateCampaign(campaign.id, name);
      this.replace(updated);
      this.editingId.set(null);
    } catch (e: any) {
      // Se deja la edición abierta: el motivo (nombre duplicado, campaña cerrada) hay
      // que poder leerlo y corregir sin perder lo escrito.
      this.error.set(e?.error?.message ?? 'No se pudieron guardar los cambios.');
    } finally {
      this.savingEdit.set(false);
    }
  }

  // --- Transición de estado ---

  async setStatus(campaign: Campaign, status: CampaignStatus): Promise<void> {
    this.error.set(null);
    try {
      const updated = await this.api.setCampaignStatus(campaign.id, status);
      this.replace(updated);
    } catch (e: any) {
      this.error.set(e?.error?.message ?? 'No se pudo cambiar el estado de la campaña.');
    }
  }

  private replace(updated: Campaign): void {
    this.campaigns.update((all) => all.map((c) => (c.id === updated.id ? updated : c)));
  }

  // --- Borrado ---

  async openDeleteModal(campaign: Campaign): Promise<void> {
    this.deleteTarget.set(campaign);
    this.deleteConfirmText.set('');
    this.deleteCounts.set(null);

    // El recuento de fragmentos no viene en la campaña: se calcula sumando los
    // documentos, para que el aviso diga con precisión qué se va a destruir.
    try {
      const documents = await this.api.listDocuments(campaign.id);
      this.deleteCounts.set({
        documents: documents.length,
        fragments: documents.reduce((total, d) => total + (d.chunkCount ?? 0), 0),
      });
    } catch {
      this.deleteCounts.set({ documents: campaign.documentCount, fragments: 0 });
    }
  }

  closeDeleteModal(): void {
    this.deleteTarget.set(null);
  }

  async confirmDelete(): Promise<void> {
    const campaign = this.deleteTarget();
    if (!campaign || !this.canConfirmDelete() || this.deleting()) return;

    this.deleting.set(true);
    try {
      await this.api.deleteCampaign(campaign.id);
      this.campaigns.update((all) => all.filter((c) => c.id !== campaign.id));
      this.deleteTarget.set(null);
    } catch (e: any) {
      this.error.set(e?.error?.message ?? 'No se pudo eliminar la campaña.');
      this.deleteTarget.set(null);
    } finally {
      this.deleting.set(false);
    }
  }

  // --- Panel de prompt ---

  async openPrompt(campaign: Campaign): Promise<void> {
    this.promptTarget.set(campaign);
    this.promptError.set(null);
    this.promptWarnings.set([]);
    this.previewQuestion = '';
    this.previewResult.set(null);
    this.previewError.set(null);
    this.applySettings(VACÍO);

    this.promptLoading.set(true);
    try {
      const [settings, versions] = await Promise.all([
        this.api.getCampaignPrompt(campaign.id),
        this.api.listCampaignPromptVersions(campaign.id),
      ]);
      this.applySettings(settings);
      this.promptVersions.set(versions);
    } catch (e: any) {
      this.promptError.set(e?.error?.message ?? 'No se pudieron cargar las instrucciones.');
    } finally {
      this.promptLoading.set(false);
    }
  }

  closePrompt(): void {
    this.promptTarget.set(null);
  }

  async savePrompt(): Promise<void> {
    const campaign = this.promptTarget();
    if (!campaign || this.promptSaving() || !this.promptEditable()) return;

    this.promptSaving.set(true);
    this.promptError.set(null);
    try {
      const result = await this.api.updateCampaignPrompt(campaign.id, this.formValue());
      this.applySettings(result.prompt);
      this.promptWarnings.set(result.warnings);
      this.promptVersions.update((all) => [
        { id: result.versionId, prompt: result.prompt, publishedBy: '', createdAtUtc: result.createdAtUtc },
        ...all,
      ]);
      // El publishedBy exacto (usuario autenticado) lo decide el servidor; se refresca
      // el historial para mostrarlo sin inventarlo en el cliente.
      this.promptVersions.set(await this.api.listCampaignPromptVersions(campaign.id));
    } catch (e: any) {
      this.promptError.set(e?.error?.message ?? 'No se pudieron guardar las instrucciones.');
    } finally {
      this.promptSaving.set(false);
    }
  }

  async restoreVersion(version: PromptVersion): Promise<void> {
    const campaign = this.promptTarget();
    if (!campaign || this.promptSaving() || !this.promptEditable()) return;

    this.promptSaving.set(true);
    this.promptError.set(null);
    try {
      const result = await this.api.restoreCampaignPromptVersion(campaign.id, version.id);
      this.applySettings(result.prompt);
      this.promptWarnings.set(result.warnings);
      this.promptVersions.set(await this.api.listCampaignPromptVersions(campaign.id));
    } catch (e: any) {
      this.promptError.set(e?.error?.message ?? 'No se pudo restaurar esta versión.');
    } finally {
      this.promptSaving.set(false);
    }
  }

  async runPreview(): Promise<void> {
    const campaign = this.promptTarget();
    const question = this.previewQuestion.trim();
    if (!campaign || !question || this.previewLoading()) return;

    this.previewLoading.set(true);
    this.previewError.set(null);
    try {
      this.previewResult.set(await this.api.previewCampaignPrompt(campaign.id, question, this.formValue()));
    } catch (e: any) {
      this.previewError.set(e?.error?.message ?? 'No se pudo generar la vista previa.');
    } finally {
      this.previewLoading.set(false);
    }
  }

  /** Resumen de una entrada del historial para la lista (sin arrow functions: el parser de plantillas de Angular no las admite). */
  versionSummary(version: PromptVersion): string {
    const p = version.prompt;
    if (p.isEmpty) return '(sin instrucciones propias)';
    const partes: string[] = [];
    if (p.tone) partes.push(p.tone);
    if (p.detailLevel) partes.push(p.detailLevel);
    return partes.length > 0 ? partes.join(' · ') : 'instrucciones propias';
  }

  private applySettings(settings: AssistantPromptSettings): void {
    this.tone = settings.tone ?? '';
    this.detailLevel = settings.detailLevel ?? '';
    this.mandatoryNotice = settings.mandatoryNotice ?? '';
    this.avoidWordsText = settings.avoidWords.join(', ');
    this.extraInstructions = settings.extraInstructions ?? '';
  }

  private formValue(): PromptFormValue {
    return {
      tone: this.tone || null,
      detailLevel: this.detailLevel || null,
      mandatoryNotice: this.mandatoryNotice.trim() || null,
      avoidWords: this.avoidWordsText.split(',').map((w) => w.trim()).filter((w) => w.length > 0),
      extraInstructions: this.extraInstructions.trim() || null,
    };
  }
}
