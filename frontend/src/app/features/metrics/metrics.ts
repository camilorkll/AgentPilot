import { DecimalPipe, PercentPipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { ApiService } from '../../core/api.service';
import { MetricsSummary } from '../../core/models';

@Component({
  selector: 'app-metrics',
  imports: [DecimalPipe, PercentPipe],
  templateUrl: './metrics.html',
  styleUrl: './metrics.css',
})
export class Metrics {
  private readonly api = inject(ApiService);

  readonly data = signal<MetricsSummary | null>(null);
  readonly operators = signal<string[]>([]);
  readonly selected = signal<string[]>([]);
  readonly error = signal<string | null>(null);

  /** Coste por modelo como lista, para pintarlo en la plantilla. */
  readonly costByModel = computed(() =>
    Object.entries(this.data()?.costByModel ?? {}).map(([model, cost]) => ({ model, cost }))
  );

  /** Máximo de preguntas en un día, para escalar las barras. */
  readonly maxPerDay = computed(() =>
    Math.max(1, ...(this.data()?.questionsPerDay ?? []).map((d) => d.count))
  );

  constructor() {
    this.refresh();
    this.loadOperators();
  }

  async refresh(): Promise<void> {
    try {
      this.data.set(await this.api.getMetrics(this.selected()));
    } catch {
      this.error.set('No se pudieron cargar las métricas.');
    }
  }

  private async loadOperators(): Promise<void> {
    try {
      this.operators.set(await this.api.getOperators());
    } catch {
      // El filtro es opcional: si falla, el resumen general sigue disponible.
    }
  }

  isSelected(userName: string): boolean {
    return this.selected().includes(userName);
  }

  /** Alterna un operador en el filtro (se pueden seleccionar varios). */
  toggle(userName: string): void {
    this.selected.update((current) =>
      current.includes(userName)
        ? current.filter((u) => u !== userName)
        : [...current, userName]
    );
    this.refresh();
  }

  clearFilter(): void {
    this.selected.set([]);
    this.refresh();
  }
}
