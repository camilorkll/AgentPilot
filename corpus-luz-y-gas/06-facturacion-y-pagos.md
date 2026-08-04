# Facturación y pagos

## Ciclo de facturación

- La factura se emite mensualmente, entre el día 1 y el día 5 de cada mes,
  con los consumos del mes anterior.
- El periodo de lectura real del contador varía según la distribuidora; si
  no hay lectura real disponible, la factura se emite con **consumo
  estimado** a partir del histórico de los últimos 12 meses.
- Cuando llega una lectura real tras varias facturas estimadas, se emite una
  factura de regularización que ajusta la diferencia, a favor o en contra
  del cliente, en la factura siguiente.

## Estimada vs. real

- Una factura estimada nunca es definitiva: si el cliente puede facilitar
  una lectura real (foto del contador con fecha), el agente puede solicitar
  la regularización inmediata sin esperar al ciclo normal.
- Los contadores inteligentes (telegestionados) no deberían generar
  facturas estimadas salvo fallo de comunicación puntual; si esto ocurre de
  forma repetida, hay que escalar el caso a la distribuidora.

## Domiciliación bancaria

- El pago domiciliado es obligatorio para acceder a la Tarifa Dúo y a
  BIENVENIDA26 (ver `catalogo-tarifas.md`); sin domiciliación, esas
  condiciones no se aplican aunque el cliente cumpla el resto de requisitos.
- Un cambio de cuenta bancaria se puede tramitar por teléfono con el IBAN
  completo y confirmación verbal del titular; se aplica a partir de la
  siguiente factura emitida, nunca a una ya generada.

## Impago y fraccionamiento

- Primer aviso de impago: SMS y email a los 10 días del vencimiento.
  Segundo aviso: carta certificada a los 20 días. Corte de suministro
  (ejecutado por la distribuidora) a partir del día 30 sin pago ni acuerdo.
- El cliente puede solicitar fraccionar una factura en hasta 3 mensualidades
  sin intereses si no tiene fraccionamientos pendientes previos; a partir
  del segundo fraccionamiento en 12 meses, se aplica un recargo del 3%.
- Un fraccionamiento en curso no evita el corte por impago de una factura
  posterior no incluida en el acuerdo.

## Reclamación de un importe facturado

- El cliente dispone de 1 año desde la fecha de factura para reclamar un
  importe que considere incorrecto (ver `atencion-al-cliente.md` para el
  plazo de resolución de la reclamación).
