import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { LoginResponse } from './models';

const TOKEN_KEY = 'agentpilot.token';
const ROLE_KEY = 'agentpilot.role';
const USER_KEY = 'agentpilot.user';

/** Estado de sesión con signals; el token se conserva en localStorage. */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);

  readonly token = signal<string | null>(localStorage.getItem(TOKEN_KEY));
  readonly role = signal<string | null>(localStorage.getItem(ROLE_KEY));
  readonly username = signal<string | null>(localStorage.getItem(USER_KEY));

  readonly isAuthenticated = computed(() => this.token() !== null);
  readonly isAdmin = computed(() => this.role() === 'admin');

  async login(username: string, password: string): Promise<void> {
    const response = await firstValueFrom(
      this.http.post<LoginResponse>('/api/v1/auth/login', { username, password })
    );

    localStorage.setItem(TOKEN_KEY, response.accessToken);
    localStorage.setItem(ROLE_KEY, response.role);
    localStorage.setItem(USER_KEY, username);

    this.token.set(response.accessToken);
    this.role.set(response.role);
    this.username.set(username);
  }

  logout(): void {
    [TOKEN_KEY, ROLE_KEY, USER_KEY].forEach((k) => localStorage.removeItem(k));
    this.token.set(null);
    this.role.set(null);
    this.username.set(null);
  }
}
