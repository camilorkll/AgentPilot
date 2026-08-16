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

/**
 * Un campo de una versión del historial, ya comparado con las instrucciones
 * vigentes: `changed` es lo que permite ver de un vistazo qué cambiaría al
 * restaurarla, en vez de tener que restaurar para averiguarlo.
 */
export interface VersionField {
  label: string;
  value: string;
  changed: boolean;
}

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

  /** Resultado de la última acción (publicar, restaurar, eliminar, cambiar el límite). */
  readonly promptNotice = signal<string | null>(null);

  /**
   * Instrucciones realmente publicadas, que NO son lo que hay en el formulario: el
   * formulario es un borrador editable. Se guarda aparte para poder decir de cada
   * versión del historial en qué se diferencia de lo que el asistente usa ahora.
   */
  readonly publishedPrompt = signal<AssistantPromptSettings>(VACÍO);

  /** Entrada del historial desplegada; solo una a la vez, para no llenar el panel. */
  readonly expandedVersionId = signal<string | null>(null);

  /**
   * Entrada que el asistente está usando: la más reciente cuyo contenido coincide con
   * lo publicado. Se busca por contenido y no se asume que sea la primera de la lista,
   * porque al eliminar entradas a mano la más reciente puede ya no ser la aplicada.
   * Solo se marca una: una restauración deja dos entradas idénticas, y señalar las dos
   * daría a entender que hay dos versiones vigentes a la vez.
   */
  readonly currentVersionId = computed(() => {
    const publicado = this.publishedPrompt();
    return this.promptVersions().find((v) => !Campaigns.difieren(v.prompt, publicado))?.id ?? null;
  });

  // --- Límite del historial (por campaña) ---
  historyLimitDraft = 5;
  readonly savingHistoryLimit = signal(false);
  readonly deletingVersionId = signal<string | null>(null);

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
    this.promptNotice.set(null);
    this.expandedVersionId.set(null);
    this.promptWarnings.set([]);
    this.previewQuestion = '';
    this.previewResult.set(null);
    this.previewError.set(null);
    this.historyLimitDraft = campaign.maxPromptVersions;
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

    const previas = this.promptVersions().length;
    this.promptSaving.set(true);
    this.promptError.set(null);
    this.promptNotice.set(null);
    try {
      const result = await this.api.updateCampaignPrompt(campaign.id, this.formValue());
      this.applySettings(result.prompt);
      this.promptWarnings.set(result.warnings);
      // El publishedBy exacto (usuario autenticado) lo decide el servidor, igual que la
      // purga por límite: se refresca el historial en vez de simularlo en el cliente.
      this.promptVersions.set(await this.api.listCampaignPromptVersions(campaign.id));
      this.promptNotice.set(
        'Instrucciones publicadas: el asistente ya responde con ellas.' +
        this.avisoPurga(previas + 1 - this.promptVersions().length)
      );
    } catch (e: any) {
      this.promptError.set(e?.error?.message ?? 'No se pudieron guardar las instrucciones.');
    } finally {
      this.promptSaving.set(false);
    }
  }

  async restoreVersion(version: PromptVersion): Promise<void> {
    const campaign = this.promptTarget();
    if (!campaign || this.promptSaving() || !this.promptEditable()) return;

    // Restaurar cambia lo que el asistente responde a partir de ese momento: se
    // confirma antes, como cualquier otra publicación, y no como un simple "ver".
    //
    // La entrada nueva empuja a las demás una posición, así que la que se restaura
    // se sale del límite (y la purga se la lleva) si ya estaba en la última plaza.
    // Prometer que "no se borra" sin comprobarlo sería mentir justo en ese caso.
    const posicion = this.promptVersions().findIndex((v) => v.id === version.id);
    const laPurgaSeLaLleva = posicion >= (campaign.maxPromptVersions - 1);

    const confirmado = confirm(
      `¿Restaurar las instrucciones del ${new Date(version.createdAtUtc).toLocaleString()}?\n\n` +
      'Pasarán a ser las instrucciones vigentes de la campaña y se añadirán al ' +
      'historial como una entrada nueva.\n\n' +
      (laPurgaSeLaLleva
        ? `Aviso: el historial está al límite (${campaign.maxPromptVersions}), así que esta ` +
          'entrada desaparecerá al añadirse la nueva. El contenido no se pierde (queda en ' +
          'la entrada nueva), pero sí la fecha y el autor originales.'
        : 'La versión de la que parte se conserva en el historial.')
    );
    if (!confirmado) return;

    const previas = this.promptVersions().length;
    this.promptSaving.set(true);
    this.promptError.set(null);
    this.promptNotice.set(null);
    try {
      const result = await this.api.restoreCampaignPromptVersion(campaign.id, version.id);
      this.applySettings(result.prompt);
      this.promptWarnings.set(result.warnings);
      this.promptVersions.set(await this.api.listCampaignPromptVersions(campaign.id));
      this.promptNotice.set(
        'Versión restaurada: ya es la vigente y se ha añadido al historial como entrada nueva.' +
        this.avisoPurga(previas + 1 - this.promptVersions().length)
      );
    } catch (e: any) {
      this.promptError.set(e?.error?.message ?? 'No se pudo restaurar esta versión.');
    } finally {
      this.promptSaving.set(false);
    }
  }

  async saveHistoryLimit(): Promise<void> {
    const campaign = this.promptTarget();
    if (!campaign || this.savingHistoryLimit() || !this.promptEditable()) return;
    if (this.historyLimitDraft < 1 || this.historyLimitDraft > 50) return;

    const previas = this.promptVersions().length;
    this.savingHistoryLimit.set(true);
    this.promptError.set(null);
    this.promptNotice.set(null);
    try {
      const updated = await this.api.updateCampaignPromptHistoryLimit(campaign.id, this.historyLimitDraft);
      this.promptTarget.set(updated);
      this.replace(updated);
      // Un límite más estricto que el histórico existente purga de inmediato en el
      // servidor: se refresca la lista para reflejar lo que realmente sobrevivió.
      this.promptVersions.set(await this.api.listCampaignPromptVersions(campaign.id));
      const limite = updated.maxPromptVersions;
      this.promptNotice.set(
        `Límite guardado: ${limite === 1 ? 'se conservará 1 versión' : `se conservarán ${limite} versiones`} como máximo.` +
        this.avisoPurga(previas - this.promptVersions().length)
      );
    } catch (e: any) {
      this.promptError.set(e?.error?.message ?? 'No se pudo guardar el límite del historial.');
    } finally {
      this.savingHistoryLimit.set(false);
    }
  }

  async deleteVersion(version: PromptVersion): Promise<void> {
    const campaign = this.promptTarget();
    if (!campaign || this.deletingVersionId() || !this.promptEditable()) return;

    const confirmed = confirm(
      `¿Seguro que quieres eliminar esta entrada del historial (${this.versionSummary(version)})?\n\n` +
      'No afecta a las instrucciones vigentes de la campaña, solo a su histórico. ' +
      'Esta acción no se puede deshacer.'
    );
    if (!confirmed) return;

    this.deletingVersionId.set(version.id);
    this.promptError.set(null);
    this.promptNotice.set(null);
    try {
      await this.api.deleteCampaignPromptVersion(campaign.id, version.id);
      this.promptVersions.update((all) => all.filter((v) => v.id !== version.id));
      if (this.expandedVersionId() === version.id) this.expandedVersionId.set(null);
      this.promptNotice.set(
        'Entrada eliminada del historial. Las instrucciones vigentes de la campaña no han cambiado.'
      );
    } catch (e: any) {
      this.promptError.set(e?.error?.message ?? 'No se pudo eliminar esta entrada del historial.');
    } finally {
      this.deletingVersionId.set(null);
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

  toggleVersion(version: PromptVersion): void {
    this.expandedVersionId.update((id) => (id === version.id ? null : version.id));
  }

  /**
   * Contenido completo de una versión, campo a campo y ya comparado con lo publicado.
   * Es lo que convierte «Restaurar» en una decisión informada: se ve qué cambiaría
   * antes de pulsarlo, sin tener que restaurar para descubrirlo.
   */
  versionFields(version: PromptVersion): VersionField[] {
    return Campaigns.comparar(version.prompt, this.publishedPrompt());
  }

  /** Es la entrada que el asistente está usando ahora mismo. */
  esVigente(version: PromptVersion): boolean {
    return this.currentVersionId() === version.id;
  }

  /** Tiene algún campo distinto de lo publicado, es decir, restaurarla cambiaría algo. */
  versionDifiere(version: PromptVersion): boolean {
    return Campaigns.difieren(version.prompt, this.publishedPrompt());
  }

  private static comparar(
    version: AssistantPromptSettings, publicado: AssistantPromptSettings
  ): VersionField[] {
    return [
      Campaigns.campo('Tono', version.tone, publicado.tone),
      Campaigns.campo('Nivel de detalle', version.detailLevel, publicado.detailLevel),
      Campaigns.campo('Aviso obligatorio', version.mandatoryNotice, publicado.mandatoryNotice),
      Campaigns.campo('Palabras a evitar', version.avoidWords.join(', '), publicado.avoidWords.join(', ')),
      Campaigns.campo('Instrucciones adicionales', version.extraInstructions, publicado.extraInstructions),
    ];
  }

  private static difieren(a: AssistantPromptSettings, b: AssistantPromptSettings): boolean {
    return Campaigns.comparar(a, b).some((f) => f.changed);
  }

  private static campo(label: string, valor: string | null, publicado: string | null): VersionField {
    const v = (valor ?? '').trim();
    return { label, value: v || '—', changed: v !== (publicado ?? '').trim() };
  }

  /** Coletilla sobre las entradas que la purga por límite se ha llevado, si se llevó alguna. */
  private avisoPurga(purgadas: number): string {
    if (purgadas <= 0) return '';
    return purgadas === 1
      ? ' Se ha eliminado la entrada más antigua del historial por el límite configurado.'
      : ` Se han eliminado las ${purgadas} entradas más antiguas del historial por el límite configurado.`;
  }

  private applySettings(settings: AssistantPromptSettings): void {
    // Solo se llama con instrucciones ya publicadas (las que devuelve el servidor al
    // abrir el panel, al guardar o al restaurar), nunca con el borrador del formulario.
    this.publishedPrompt.set(settings);
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
