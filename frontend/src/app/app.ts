import { Component, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { ApiService } from './core/api.service';
import { AuthService } from './core/auth.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './app.html',
  styleUrl: './app.css',
})
export class App {
  protected readonly auth = inject(AuthService);
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);

  /** Última comprobación, para no repetirla cuando los dos eventos saltan juntos. */
  private ultimaComprobacion = 0;

  constructor() {
    // Un token desplazado no se detecta solo: el JWT ya está en el navegador y nadie
    // avisa desde el servidor. Sin esto, la pantalla del operador desplazado se queda
    // ahí, con su chat a la vista, aparentando estar dentro hasta que él intente algo.
    //
    // Hacen falta LOS DOS eventos, y con uno solo no bastaba:
    //   - `visibilitychange` cubre cambiar de pestaña o minimizar.
    //   - `focus` cubre alternar entre dos VENTANAS distintas, que es como se prueba
    //     esto de verdad: con dos ventanas a la vista ninguna pestaña llega a estar
    //     oculta, así que visibilitychange no salta nunca y ambas seguían abiertas.
    //
    // Se descarta un temporizador: no tiene sentido preguntar mientras nadie mira, con
    // esta pantalla abierta toda la jornada.
    document.addEventListener('visibilitychange', () => this.comprobarSesion());
    window.addEventListener('focus', () => this.comprobarSesion());
  }

  private comprobarSesion(): void {
    if (document.visibilityState !== 'visible' || !this.auth.isAuthenticated()) return;

    // Cambiar de pestaña dispara los dos eventos; sin esto se pediría dos veces.
    const ahora = Date.now();
    if (ahora - this.ultimaComprobacion < 1000) return;
    this.ultimaComprobacion = ahora;

    // Cualquier petición autenticada vale: si el token ya no sirve, el interceptor
    // cierra la sesión y explica el motivo. El error se ignora aquí a propósito.
    this.api.listActiveCampaigns().catch(() => { /* ya lo ha atendido el interceptor */ });
  }

  logout(): void {
    this.auth.logout();
    this.router.navigate(['/login']);
  }
}
