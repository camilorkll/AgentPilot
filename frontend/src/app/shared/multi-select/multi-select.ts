import { ElementRef, HostListener, Component, computed, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

/**
 * Selector múltiple con búsqueda, para filtros con decenas de opciones (operadores,
 * campañas…) donde un botón por opción deja de ser usable. Sin dependencias externas:
 * el frontend no tiene librería de UI y meter una por un desplegable desentonaría.
 */
@Component({
  selector: 'app-multi-select',
  imports: [FormsModule],
  templateUrl: './multi-select.html',
  styleUrl: './multi-select.css',
})
export class MultiSelect {
  private readonly host = inject(ElementRef<HTMLElement>);

  readonly options = input.required<string[]>();
  readonly selected = input<string[]>([]);
  readonly placeholder = input('Todos');
  readonly selectedChange = output<string[]>();

  readonly open = signal(false);
  readonly query = signal('');
  readonly focusedIndex = signal(-1);

  readonly filteredOptions = computed(() => {
    const q = this.query().trim().toLowerCase();
    const opts = this.options();
    return q ? opts.filter((o) => o.toLowerCase().includes(q)) : opts;
  });

  readonly allSelected = computed(
    () => this.options().length > 0 && this.selected().length === this.options().length
  );

  /** Cierra al hacer clic fuera: patrón estándar de desplegable. */
  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    if (this.open() && !this.host.nativeElement.contains(event.target as Node)) {
      this.close();
    }
  }

  toggleOpen(): void {
    this.open.update((v) => !v);
    if (this.open()) {
      this.query.set('');
      this.focusedIndex.set(-1);
    }
  }

  close(): void {
    this.open.set(false);
  }

  isSelected(option: string): boolean {
    return this.selected().includes(option);
  }

  toggle(option: string): void {
    const current = this.selected();
    const next = current.includes(option)
      ? current.filter((o) => o !== option)
      : [...current, option];
    this.selectedChange.emit(next);
  }

  remove(option: string, event: Event): void {
    event.stopPropagation(); // no reabrir el desplegable al quitar una etiqueta
    this.selectedChange.emit(this.selected().filter((o) => o !== option));
  }

  selectAll(): void {
    this.selectedChange.emit([...this.options()]);
  }

  selectNone(): void {
    this.selectedChange.emit([]);
  }

  onKeydown(event: KeyboardEvent): void {
    if (event.key === 'Escape') {
      event.stopPropagation();
      this.close();
      return;
    }
    if (!this.open()) return;

    const opts = this.filteredOptions();
    if (event.key === 'ArrowDown') {
      event.preventDefault();
      this.focusedIndex.update((i) => Math.min(i + 1, opts.length - 1));
    } else if (event.key === 'ArrowUp') {
      event.preventDefault();
      this.focusedIndex.update((i) => Math.max(i - 1, 0));
    } else if (event.key === 'Enter') {
      event.preventDefault();
      const option = opts[this.focusedIndex()];
      if (option) this.toggle(option);
    }
  }
}
