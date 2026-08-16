import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { AuthService } from './auth.service';

/** Añade el JWT a cada petición y cierra sesión si el token deja de ser válido. */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const token = auth.token();

  const request = token
    ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
    : req;

  return next(request).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 401 && !req.url.includes('/auth/login')) {
        // Que te echen sin explicación se lee como un fallo de la aplicación. Si el motivo
        // es que el mismo operador entró en otro sitio, hay que decirlo: o fue él y lo
        // entiende, o no fue él y es justo lo que le interesa saber.
        auth.cerrarSesionPorTokenInvalido(
          error.headers.get('X-Auth-Error') === 'session_superseded');
      }
      return throwError(() => error);
    })
  );
};
