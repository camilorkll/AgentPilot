import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { ApiService } from '../../core/api.service';
import { DocumentSummary } from '../../core/models';

@Component({
  selector: 'app-documents',
  imports: [DatePipe],
  templateUrl: './documents.html',
  styleUrl: './documents.css',
})
export class Documents {
  private readonly api = inject(ApiService);

  readonly documents = signal<DocumentSummary[]>([]);
  readonly uploading = signal(false);
  readonly error = signal<string | null>(null);

  constructor() {
    this.refresh();
  }

  async refresh(): Promise<void> {
    try {
      this.documents.set(await this.api.listDocuments());
    } catch {
      this.error.set('No se pudo cargar el listado de documentos.');
    }
  }

  async onFileSelected(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;

    this.error.set(null);
    this.uploading.set(true);
    try {
      await this.upload(file, false);
    } catch (e: any) {
      if (e?.status === 409) {
        // Ya existe: preguntamos antes de sustituirlo, porque reemplazar borra
        // el documento anterior y sus fragmentos indexados.
        const replace = confirm(
          `«${file.name}» ya está en la base de conocimiento.\n\n` +
          `¿Quieres reemplazarlo? Se eliminará la versión anterior y se volverá a indexar.`
        );
        if (replace) {
          try {
            await this.upload(file, true);
          } catch {
            this.error.set('No se pudo reemplazar el documento.');
          }
        }
      } else {
        this.error.set('No se pudo subir el documento (¿formato admitido? PDF o Markdown).');
      }
    } finally {
      this.uploading.set(false);
      input.value = '';
    }
  }

  private async upload(file: File, replace: boolean): Promise<void> {
    await this.api.uploadDocument(file, { replace });
    // La ingesta es asíncrona: refrescamos varias veces para ver el progreso.
    await this.refresh();
    for (const delay of [2000, 4000, 6000, 9000]) {
      setTimeout(() => this.refresh(), delay);
    }
  }

  async remove(document: DocumentSummary): Promise<void> {
    try {
      await this.api.deleteDocument(document.id);
      await this.refresh();
    } catch {
      this.error.set('No se pudo eliminar el documento.');
    }
  }
}
