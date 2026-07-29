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
  }

  async refresh(): Promise<void> {
    try {
      this.data.set(await this.api.getMetrics());
    } catch {
      this.error.set('No se pudieron cargar las métricas.');
    }
  }
}
