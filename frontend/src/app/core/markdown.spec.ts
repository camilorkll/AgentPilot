import { renderMarkdown } from './markdown';

describe('renderMarkdown', () => {
  it('convierte el caso que motivó todo esto', () => {
    // Respuesta real del argumentario de retención: se leía con los asteriscos a la vista.
    const html = renderMarkdown(
      '1. **Revisión de tarifa** (downgrade) - aplica a cualquier cliente.\n' +
      '2. **20% de descuento** - antigüedad mayor a 12 meses.'
    );

    expect(html).toContain('<ol>');
    expect(html).toContain('<strong>Revisión de tarifa</strong>');
    expect(html).not.toContain('**');
  });

  it('respeta las citas entre corchetes', () => {
    // Son la prueba de que la respuesta está fundamentada: no pueden desaparecer.
    expect(renderMarkdown('El cambio es gratuito [1], [2].')).toContain('[1], [2]');
  });

  describe('seguridad', () => {
    // El texto viene de un LLM que ha leído documentos de campaña: contenido no confiable.

    it('neutraliza el HTML del texto', () => {
      const html = renderMarkdown('<script>alert(1)</script>');

      expect(html).not.toContain('<script>');
      expect(html).toContain('&lt;script&gt;');
    });

    it('no deja construir una etiqueta a través de una marca', () => {
      // Si se escapara DESPUÉS de transformar, esto produciría un <img> con onerror.
      const html = renderMarkdown('**<img src=x onerror=alert(1)>**');

      // Lo que importa es que no haya etiqueta: el texto «onerror=alert(1)» sí aparece,
      // pero como contenido escapado y visible, que es inofensivo. Comprobar que la
      // cadena no está sería exigir de más y dar por malo un resultado correcto.
      expect(html).not.toContain('<img');
      expect(html).toContain('&lt;img src=x onerror=alert(1)&gt;');
      expect(html).toContain('<strong>');
    });

    it('no genera enlaces aunque el texto traiga sintaxis de enlace', () => {
      const html = renderMarkdown('[pincha aquí](javascript:alert(1))');

      expect(html).not.toContain('<a');
      expect(html).not.toContain('href');
    });

    it('escapa las comillas, que cerrarían un atributo', () => {
      expect(renderMarkdown('dice "hola"')).toContain('&quot;hola&quot;');
    });
  });

  describe('estructura', () => {
    it('separa párrafos por línea en blanco y conserva el salto simple', () => {
      const html = renderMarkdown('Primero\nsegunda línea\n\nOtro párrafo');

      expect(html).toBe('<p>Primero<br>segunda línea</p><p>Otro párrafo</p>');
    });

    it('distingue lista con viñetas de lista numerada', () => {
      expect(renderMarkdown('- uno\n- dos')).toBe('<ul><li>uno</li><li>dos</li></ul>');
      expect(renderMarkdown('1. uno\n2. dos')).toBe('<ol><li>uno</li><li>dos</li></ol>');
    });

    it('cierra una lista cuando el texto vuelve a párrafo', () => {
      const html = renderMarkdown('- uno\n\nTexto suelto');

      expect(html).toBe('<ul><li>uno</li></ul><p>Texto suelto</p>');
    });

    it('no reinterpreta las marcas dentro de un fragmento de código', () => {
      expect(renderMarkdown('usa `**esto**` literal')).toContain('<code>**esto**</code>');
    });
  });

  describe('texto a medias, mientras se escribe la respuesta', () => {
    it('deja literal una negrita sin cerrar', () => {
      // Llega token a token: hay instantes con la marca abierta y no debe romperse.
      expect(renderMarkdown('1. **Revisión de ta')).toContain('**Revisión de ta');
    });

    it('no rompe con texto vacío', () => {
      expect(renderMarkdown('')).toBe('');
    });
  });
});
