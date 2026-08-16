/**
 * Convierte a HTML el subconjunto de Markdown que escribe el asistente.
 *
 * Por qué existe: el modelo responde con negritas y listas por defecto, y la interfaz las
 * pintaba en crudo — el agente leía «1. **Revisión de tarifa** (downgrade)…» con los
 * asteriscos a la vista, justo en las respuestas más útiles (argumentarios, procedimientos
 * por pasos), que son las que llevan estructura.
 *
 * Por qué no una librería: el texto que se renderiza viene de un LLM que ha leído
 * documentos de campaña, así que es contenido no confiable por definición — el propio
 * corpus incluye un documento de prueba con una inyección. Un generador de Markdown de
 * propósito general emite HTML arbitrario (enlaces, imágenes, atributos) y obliga a
 * confiar en su saneado. Aquí el conjunto de etiquetas que se pueden producir es cerrado y
 * se lee de un vistazo: <p>, <br>, <ul>, <ol>, <li>, <strong>, <em> y <code>.
 *
 * La seguridad se apoya en tres capas, en este orden:
 *
 *   1. **Se escapa primero.** Todo `&`, `<`, `>` y `"` del texto se neutraliza ANTES de
 *      tocar nada. A partir de ese punto es imposible que el contenido produzca una
 *      etiqueta: las que aparecen luego las pone este fichero, no el texto.
 *   2. **No se generan enlaces ni imágenes.** El asistente cita con `[1]`, no con URLs, así
 *      que no hace falta emitir `<a>` ni `<img>` y la superficie de `javascript:` y de
 *      `onerror=` desaparece entera en vez de tener que filtrarse.
 *   3. **Angular vuelve a sanear.** El binding `[innerHTML]` pasa por DomSanitizer, que
 *      quitaría cualquier cosa peligrosa que se colara pese a lo anterior.
 *
 * Fuera de alcance a propósito: tablas, encabezados, citas en bloque y enlaces. El
 * asistente no los usa al responder (las tablas del corpus llegan como dato dentro de los
 * fragmentos, no en la respuesta), y cada uno añadiría casos que probar sin mejorar lo que
 * el agente lee durante una llamada.
 */

/** Neutraliza el HTML del texto. Todo lo demás depende de que esto vaya primero. */
function escapar(texto: string): string {
  return texto
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
}

/** Negritas y cursivas. Se aplica solo a lo que queda fuera de un fragmento de código. */
function marcas(escapado: string): string {
  return escapado
    .replace(/\*\*([^*\n]+)\*\*/g, '<strong>$1</strong>')
    // Cursiva solo con un asterisco: el guion bajo aparece en nombres de fichero y de
    // variables del corpus, y convertirlo en cursiva rompería más de lo que arregla.
    .replace(/(^|[^*])\*([^*\n]+)\*/g, '$1<em>$2</em>');
}

/**
 * Marcas dentro de una línea. Se aplican sobre texto YA escapado.
 *
 * La línea se parte por los fragmentos entre acentos graves y solo se transforma lo que
 * queda fuera, porque el contenido de un fragmento de código debe verse literal: en
 * `` `**esto**` `` los asteriscos son parte del ejemplo. Convertirlo antes a `<code>` no
 * bastaba — la pasada de negritas seguía viendo esos asteriscos dentro de la etiqueta y
 * los transformaba igual. Lo detectó un test, no la lectura del código.
 */
function enLinea(escapado: string): string {
  // split() con grupo de captura deja lo capturado en las posiciones impares.
  return escapado
    .split(/`([^`\n]+)`/g)
    .map((parte, i) => (i % 2 === 1 ? `<code>${parte}</code>` : marcas(parte)))
    .join('');
}

const VINETA = /^\s*[-*]\s+(.*)$/;
const NUMERADA = /^\s*\d+[.)]\s+(.*)$/;

/**
 * Renderiza el texto del asistente. Devuelve HTML listo para `[innerHTML]`.
 *
 * Tolera texto a medias: mientras la respuesta se está escribiendo llegan marcas sin
 * cerrar (`**Revisión` antes de su segundo `**`), y esas se quedan literales hasta que
 * cierran, que es como se comporta cualquier chat.
 */
export function renderMarkdown(texto: string): string {
  if (!texto) return '';

  const lineas = escapar(texto).split('\n');
  const salida: string[] = [];
  let lista: 'ul' | 'ol' | null = null;
  let parrafo: string[] = [];

  const cerrarParrafo = () => {
    if (parrafo.length === 0) return;
    salida.push(`<p>${enLinea(parrafo.join('<br>'))}</p>`);
    parrafo = [];
  };

  const cerrarLista = () => {
    if (lista === null) return;
    salida.push(`</${lista}>`);
    lista = null;
  };

  for (const linea of lineas) {
    const vineta = linea.match(VINETA);
    const numerada = linea.match(NUMERADA);
    const tipo = vineta ? 'ul' : numerada ? 'ol' : null;

    if (tipo) {
      cerrarParrafo();
      if (lista !== tipo) {
        cerrarLista();
        salida.push(`<${tipo}>`);
        lista = tipo;
      }
      salida.push(`<li>${enLinea((vineta ?? numerada)![1])}</li>`);
      continue;
    }

    cerrarLista();

    // Una línea en blanco separa párrafos; dentro de uno, el salto se conserva.
    if (linea.trim() === '') cerrarParrafo();
    else parrafo.push(linea);
  }

  cerrarParrafo();
  cerrarLista();
  return salida.join('');
}
