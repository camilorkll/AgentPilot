#!/usr/bin/env bash
# Sube el corpus de ejemplo a una campaña. Sin esto, un arranque limpio deja la campaña
# TeleNova creada pero VACÍA: la migración siembra la campaña, no sus documentos, porque
# indexar exige llamadas de embeddings y no puede depender de que haya clave de OpenAI
# configurada en el momento de migrar.
#
#   ./scripts/poblar-corpus.sh                        # TeleNova en local
#   ./scripts/poblar-corpus.sh https://mi-despliegue  # otro destino
#
# Requiere que la API esté arriba y con OpenAI__ApiKey configurada.
set -euo pipefail

BASE="${1:-http://localhost:8080}"
CAMPAIGN="${2:-11111111-1111-1111-1111-111111111111}"   # TeleNova, sembrada por migración
CARPETA="${3:-corpus}"
USUARIO="${ADMIN_USER:-admin}"
CLAVE="${ADMIN_PASSWORD:-admin1234}"

echo "Destino: $BASE"
echo "Campaña: $CAMPAIGN"

TOKEN=$(curl -fsS -X POST "$BASE/api/v1/auth/login" \
  -H 'Content-Type: application/json' \
  -d "{\"userName\":\"$USUARIO\",\"password\":\"$CLAVE\"}" \
  | grep -o '"accessToken":"[^"]*"' | cut -d'"' -f4)

if [ -z "$TOKEN" ]; then
  echo "No se pudo autenticar como $USUARIO." >&2
  exit 1
fi

subidos=0
for fichero in "$CARPETA"/*.md; do
  nombre=$(basename "$fichero")
  [ "$nombre" = "README.md" ] && continue          # el README de la carpeta no es corpus

  # replace=true para que el script sea repetible: relanzarlo reprocesa en vez de fallar
  # por duplicado, y una ingesta que falle no borra lo que ya había (ADR-018).
  curl -fsS -o /dev/null -X POST "$BASE/api/v1/documents" \
    -H "Authorization: Bearer $TOKEN" \
    -F "file=@$fichero" -F "campaignId=$CAMPAIGN" -F 'replace=true'
  echo "  subido $nombre"
  subidos=$((subidos + 1))
done

echo
echo "$subidos documentos encolados. La indexación corre en segundo plano:"
echo "consulta el estado en $BASE/documents (como administrador)."
