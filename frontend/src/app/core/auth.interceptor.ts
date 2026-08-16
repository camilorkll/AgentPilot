import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { AuthService } from './auth.service';

/** Añade el JWT a cada petición y cierra sesión si el token deja de ser válido. */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const token = auth.token();

  const request = token
    ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
    : req;

  return next(request).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 401 && !req.url.includes('/auth/login')) {
        auth.logout();
        // Que te echen sin explicación se lee como un fallo de la aplicación. Si el motivo
        // es que el mismo operador entró en otro sitio, hay que decirlo: o fue él y lo
        // entiende, o no fue él y es justo lo que le interesa saber.
        const desplazada = error.headers.get('X-Auth-Error') === 'session_superseded';
        router.navigate(['/login'], desplazada ? { queryParams: { motivo: 'sesion' } } : {});
      }
      return throwError(() => error);
    })
  );
};
