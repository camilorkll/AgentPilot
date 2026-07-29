import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../core/auth.service';

@Component({
  selector: 'app-login',
  imports: [FormsModule],
  templateUrl: './login.html',
  styleUrl: './login.css',
})
export class Login {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  username = '';
  password = '';
  readonly error = signal<string | null>(null);
  readonly loading = signal(false);

  async submit(): Promise<void> {
    this.error.set(null);
    this.loading.set(true);
    try {
      await this.auth.login(this.username, this.password);
      this.router.navigate(['/chat']);
    } catch {
      this.error.set('Usuario o contraseña incorrectos.');
    } finally {
      this.loading.set(false);
    }
  }

  /** Rellena las credenciales de demo (documentadas en el README). */
  fill(role: 'admin' | 'agent'): void {
    this.username = role === 'admin' ? 'admin' : 'agente';
    this.password = role === 'admin' ? 'admin1234' : 'agente1234';
  }
}
