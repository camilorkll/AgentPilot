/**
 * Comprueba que todo campo declarado en `required` exista entre las `properties`
 * del esquema.
 *
 * Spectral resuelve los $ref y valida la estructura OpenAPI, pero no detecta este
 * caso: un `required: [campaignId]` en un esquema que no declara `campaignId` es
 * OpenAPI válido y Swagger UI lo pinta sin queja. El contrato queda mintiendo —
 * promete un campo obligatorio que no describe — y quien genere cliente a partir
 * de él se encuentra el hueco en tiempo de compilación.
 *
 * Los objetos con `required: true` (parámetros, cuerpos de petición) se ignoran:
 * ahí `required` es un booleano y significa otra cosa.
 */
export default (schema) => {
  if (!schema || !Array.isArray(schema.required)) return;

  const properties = Object.keys(schema.properties ?? {});

  return schema.required
    .filter((campo) => !properties.includes(campo))
    .map((campo) => ({
      message: `"${campo}" se declara en required pero no está entre las properties`,
    }));
};
