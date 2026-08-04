import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/api.service';
import { Campaign, CampaignStatus } from '../../core/models';

/** Estados desde los que tiene sentido cada acción, para no mostrar botones que fallarían. */
const ACCIONES_POR_ESTADO: Record<CampaignStatus, { label: string; target: CampaignStatus }[]> = {
  active: [{ label: 'Desactivar', target: 'inactive' }],
  inactive: [
    { label: 'Activar', target: 'active' },
    { label: 'Cerrar', target: 'closed' },
  ],
  closed: [{ label: 'Reabrir', target: 'inactive' }],
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
  newInstructions = '';
  readonly creating = signal(false);

  // --- Edición en línea (una fila a la vez) ---
  readonly editingId = signal<string | null>(null);
  draftName = '';
  draftInstructions = '';
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
      const created = await this.api.createCampaign(name, this.newInstructions.trim() || null);
      this.campaigns.update((all) => [...all, created].sort((a, b) => a.name.localeCompare(b.name)));
      this.newName = '';
      this.newInstructions = '';
    } catch (e: any) {
      this.error.set(e?.error?.message ?? 'No se pudo crear la campaña.');
    } finally {
      this.creating.set(false);
    }
  }

  // --- Edición en línea ---

  startEdit(campaign: Campaign): void {
    this.editingId.set(campaign.id);
    this.draftName = campaign.name;
    this.draftInstructions = campaign.assistantInstructions ?? '';
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
      const updated = await this.api.updateCampaign(
        campaign.id, name, this.draftInstructions.trim() || null
      );
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
}
