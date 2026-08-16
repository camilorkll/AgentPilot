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

  constructor() {
    // Un token desplazado no se detecta solo: el JWT ya está en el navegador y nadie
    // avisa desde el servidor. Sin esto, la pestaña del operador desplazado se quedaba
    // ahí, con su chat a la vista, aparentando estar dentro hasta que él intentara
    // algo. Al volver a mirarla se comprueba, que es justo cuando le importa.
    //
    // Se elige `visibilitychange` y no un temporizador para no pedir nada mientras
    // nadie mira: un agente tiene esta pantalla abierta toda la jornada.
    document.addEventListener('visibilitychange', () => {
      if (document.visibilityState !== 'visible' || !this.auth.isAuthenticated()) return;

      // Cualquier petición autenticada vale: si el token ya no sirve, el interceptor
      // cierra la sesión y explica el motivo. El error se ignora aquí a propósito.
      this.api.listActiveCampaigns().catch(() => { /* ya lo ha atendido el interceptor */ });
    });
  }

  logout(): void {
    this.auth.logout();
    this.router.navigate(['/login']);
  }
}
