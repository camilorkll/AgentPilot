import { DecimalPipe, PercentPipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/api.service';
import { Campaign, DailyOperatorUsage, MetricsSummary, MonthlyTotal } from '../../core/models';
import { MultiSelect } from '../../shared/multi-select/multi-select';

const VIEW_KEY = 'agentpilot.metrics.view';

/** Fila del día en la vista Agente → Días: sus operadores, ya agrupados por mes. */
interface AgentMonthSection {
  month: string;
  days: DailyOperatorUsage[];
  total: MonthlyTotal | null;
}
interface AgentGroup {
  userName: string;
  sections: AgentMonthSection[];
}

/** Fila de la vista Día → Agentes: o bien un día con sus operadores, o un cierre de mes. */
type DayRow =
  | { kind: 'day'; date: string; agents: DailyOperatorUsage[] }
  | { kind: 'monthTotal'; month: string; total: MonthlyTotal };

@Component({
  selector: 'app-metrics',
  imports: [DecimalPipe, PercentPipe, FormsModule, MultiSelect],
  templateUrl: './metrics.html',
  styleUrl: './metrics.css',
})
export class Metrics {
  private readonly api = inject(ApiService);

  readonly data = signal<MetricsSummary | null>(null);
  readonly operators = signal<string[]>([]);
  readonly selectedOperators = signal<string[]>([]);
  readonly campaigns = signal<Campaign[]>([]);
  readonly campaignId = signal<string>(''); // '' = todas, 'none' = sin campaña, o un id
  readonly monthFrom = signal<string>('');
  readonly monthTo = signal<string>('');
  readonly error = signal<string | null>(null);
  readonly exporting = signal(false);

  /** Vista del detalle: por agente o por día. Se recuerda entre sesiones. */
  readonly view = signal<'byAgent' | 'byDay'>(
    (localStorage.getItem(VIEW_KEY) as 'byAgent' | 'byDay' | null) ?? 'byAgent'
  );

  /** Coste por modelo como lista, para pintarlo en la plantilla. */
  readonly costByModel = computed(() =>
    Object.entries(this.data()?.costByModel ?? {}).map(([model, cost]) => ({ model, cost }))
  );

  readonly costByCampaign = computed(() =>
    Object.entries(this.data()?.costByCampaign ?? {}).map(([campaign, cost]) => ({ campaign, cost }))
  );

  /** Máximo de preguntas en un día, para escalar las barras. */
  readonly maxPerDay = computed(() =>
    Math.max(1, ...(this.data()?.questionsPerDay ?? []).map((d) => d.count))
  );

  /** Vista 1 — Agente → Días: cada agente con sus días, agrupados por mes y su total. */
  readonly byAgentView = computed<AgentGroup[]>(() => {
    const daily = this.data()?.dailyByOperator ?? [];
    const monthly = this.data()?.monthlyTotals ?? [];

    const byAgent = new Map<string, DailyOperatorUsage[]>();
    for (const row of daily) {
      const list = byAgent.get(row.userName);
      list ? list.push(row) : byAgent.set(row.userName, [row]);
    }

    return [...byAgent.entries()]
      .sort(([a], [b]) => a.localeCompare(b))
      .map(([userName, rows]) => {
        const sorted = [...rows].sort((a, b) => a.date.localeCompare(b.date));
        const months = groupByMonth(sorted);
        const sections: AgentMonthSection[] = [...months.entries()].map(([month, days]) => ({
          month,
          days,
          total: monthly.find((m) => m.userName === userName && m.month === month) ?? null,
        }));
        return { userName, sections };
      });
  });

  /** Vista 2 — Día → Agentes: cada día con sus agentes, y un cierre al terminar cada mes. */
  readonly byDayView = computed<DayRow[]>(() => {
    const daily = this.data()?.dailyByOperator ?? [];
    const monthly = this.data()?.monthlyTotals ?? [];

    const byDay = new Map<string, DailyOperatorUsage[]>();
    for (const row of daily) {
      const list = byDay.get(row.date);
      list ? list.push(row) : byDay.set(row.date, [row]);
    }

    const sortedDays = [...byDay.keys()].sort();
    const rows: DayRow[] = [];
    let currentMonth: string | null = null;

    const closeMonth = (month: string) => {
      const total = monthly.find((m) => m.userName === null && m.month === month);
      if (total) rows.push({ kind: 'monthTotal', month, total });
    };

    for (const date of sortedDays) {
      const month = date.slice(0, 7);
      if (currentMonth !== null && month !== currentMonth) closeMonth(currentMonth);
      const agents = [...byDay.get(date)!].sort((a, b) => a.userName.localeCompare(b.userName));
      rows.push({ kind: 'day', date, agents });
      currentMonth = month;
    }
    if (currentMonth !== null) closeMonth(currentMonth);

    return rows;
  });

  constructor() {
    this.refresh();
    this.loadOperators();
    this.loadCampaigns();
  }

  setView(view: 'byAgent' | 'byDay'): void {
    this.view.set(view);
    localStorage.setItem(VIEW_KEY, view);
  }

  async refresh(): Promise<void> {
    this.error.set(null);
    try {
      const summary = await this.api.getMetrics(this.currentFilter());
      this.data.set(summary);
      // Los meses que de verdad se aplicaron (con los valores por defecto ya resueltos
      // por el servidor) se reflejan en los selectores, para no dejarlos vacíos
      // mientras los datos ya muestran un mes concreto.
      this.monthFrom.set(summary.monthFrom ?? '');
      this.monthTo.set(summary.monthTo ?? '');
    } catch {
      this.error.set('No se pudieron cargar las métricas.');
    }
  }

  private currentFilter() {
    return {
      operators: this.selectedOperators(),
      monthFrom: this.monthFrom() || undefined,
      monthTo: this.monthTo() || undefined,
      campaignId: this.campaignId() || undefined,
    };
  }

  private async loadOperators(): Promise<void> {
    try {
      this.operators.set(await this.api.getOperators());
    } catch {
      // El filtro es opcional: si falla, el resumen general sigue disponible.
    }
  }

  private async loadCampaigns(): Promise<void> {
    try {
      this.campaigns.set(await this.api.listCampaigns());
    } catch {
      // Igual que los operadores: el filtro de campaña es opcional.
    }
  }

  onOperatorsChange(selected: string[]): void {
    this.selectedOperators.set(selected);
    this.refresh();
  }

  onMonthFromChange(value: string): void {
    this.monthFrom.set(value);
    this.refresh();
  }

  onMonthToChange(value: string): void {
    this.monthTo.set(value);
    this.refresh();
  }

  onCampaignChange(value: string): void {
    this.campaignId.set(value);
    this.refresh();
  }

  async exportCsv(): Promise<void> {
    this.exporting.set(true);
    try {
      await this.api.exportMetricsCsv(this.currentFilter());
    } catch {
      this.error.set('No se pudo exportar el CSV.');
    } finally {
      this.exporting.set(false);
    }
  }
}

/** Agrupa filas ya ordenadas por fecha en sub-listas por mes (YYYY-MM), en orden. */
function groupByMonth(sortedRows: DailyOperatorUsage[]): Map<string, DailyOperatorUsage[]> {
  const months = new Map<string, DailyOperatorUsage[]>();
  for (const row of sortedRows) {
    const month = row.date.slice(0, 7);
    const list = months.get(month);
    list ? list.push(row) : months.set(month, [row]);
  }
  return months;
}
