# Observaciones sobre modalidades de contratación (SECOP)

## 1. Las modalidades de contratación, una por una

Dos mundos: Ley 80 (Estatuto General, reglas estandarizadas y competencia pública) y régimen especial (cada entidad con su manual y derecho privado). Más un tipo que no es para ofertar.

### 1.1 Modalidades Ley 80 (Estatuto General)

| Modalidad | Para qué se usa | Cuantía / umbral | Ventana típica | Dónde está el cierre |
|---|---|---|---|---|
| Licitación pública | Obras y compras grandes | Por encima de la menor cuantía | Semanas | Hito "Presentación de Ofertas" |
| Selección abreviada – menor cuantía | Bienes/servicios de cuantía media | Hasta el límite de menor cuantía (aparece como [Límite de contratación Menor Cuantía]) | ~5–15 días | Hito "Presentación de Ofertas" |
| Selección abreviada – subasta inversa | Bienes uniformes | Variable | ~5–15 días | Hito "Presentación de Ofertas" |
| Concurso de méritos | Servicios intelectuales / consultoría | Variable | Semanas | Hito "Presentación de Ofertas" |
| Mínima cuantía | Compras pequeñas | ≤10% de la menor cuantía ([Límite de contratación Cantidad mínima]) | 3–7 días | Hito "Presentación de Ofertas" |
| Contratación directa | Casos sin competencia (urgencia, único proveedor) | Cualquiera | — | Suele publicarse ya decidido |

En Ley 80 el cronograma es guía confiable (sección 6).

### 1.2 Régimen especial

No es una "modalidad de selección" sino un régimen distinto. Lo identifica el campo `[Tipo de proceso] = "Contratación régimen especial"`.

Aparte, el campo `[¿Uso del módulo de forma publicitaria?]` indica cómo usa la entidad el módulo de SECOP, y de eso depende si es ofertable:

- **Publicitaria = Sí**: la entidad solo publica algo ya decidido. **No ofertable.**
- **Publicitaria = No (transaccional)**: la entidad corre el proceso en la plataforma. Puede ser ofertable; revisar.

**Caso particular:** `[Tipo de contrato] = "Decreto 092 de 2017"`. Es una vía para que el Estado contrate solo con entidades privadas sin ánimo de lucro (ESAL) de reconocida idoneidad, para programas de interés público del Plan de Desarrollo (art. 355 de la Constitución). Puede haber proceso competitivo (cuando hay varias ESAL idóneas) o no (p. ej. si la ESAL aporta ≥30% del valor). Para una empresa comercial normal no es un objetivo: no califica como ESAL. El sistema debería marcar estos procesos como "solo ESAL" y descartarlos salvo que el usuario lo sea.

### 1.3 Solicitud de información a los proveedores (no es selección)

`[Tipo de proceso] = "Solicitud de información a los Proveedores"`. Es un sondeo de mercado (RFI): la entidad reúne información para estructurar un proceso futuro. No se adjudica nada. El cronograma habla de "fecha límite para responder la solicitud de información", no de ofertas. **No ofertable**, pero sirve como aviso temprano (sección 4).

---

## 2. Problema del campo "Modalidad: Otro"

**Qué pasa hoy.** La búsqueda muestra "Modalidad: Otro" en lugar del tipo real. Eso esconde lo más importante: muchos de esos "Otro" son régimen especial, que debe tratarse aparte; y algunos son RFI, que ni siquiera son ofertables.

**Cómo resolverlo (lógica exacta):**

1. Extraer siempre el campo `[Tipo de proceso]` literal y guardarlo. Nunca poner "Otro" por defecto.
2. Mapear ese valor a una categoría interna:
   - Contiene "régimen especial" → categoría `REGIMEN_ESPECIAL` → ir a sección 3.
   - Contiene "Solicitud de información" → categoría `RFI` → no ofertable.
   - "Mínima cuantía", "Selección abreviada", "Licitación", "Concurso de méritos", "Contratación directa" → categoría `LEY_80` → usar cronograma (sección 6).
3. Si el campo viene realmente vacío en la API, ir al PDF antes de rotularlo "Otro".

**Resultado:** el usuario ve "Régimen especial (publicitario)" o "Mínima cuantía", no "Otro".

---

## 3. Régimen especial: descarte y lista de entidades

### 3.1 Por qué se descarta

El régimen especial está exceptuado de la Ley 80 (Estatuto General). Cada entidad contrata según su propio manual y el derecho privado, muchas veces por invitación o decisión directa, y desde la Ley 2195 de 2022 (art. 53) solo está obligada a publicar su actividad en SECOP II — publicar no es competir. Por eso muchos de estos registros aparecen cuando el contrato ya está hecho, sin ventana real para un proveedor externo.

### 3.2 Regla y salvedad

**Regla por defecto:** descartar los procesos de régimen especial de la lista de oportunidades.

**Salvedad (no botar lo bueno):** separar por subtipo.

- `[¿Uso del módulo de forma publicitaria?] = Sí` → publicitario → **descartar siempre**.
- `= No` + hay actividad transaccional (observaciones, oferentes) y sin adjudicatario aún → con ofertas → **revisar, puede ser oportunidad real** (FINDETER, $2.150M, vivo).

Dejarlo como interruptor configurable: "descartar todo el régimen especial" (más simple, pierde leads grandes) vs. "descartar solo publicitario" (recomendado).

### 3.3 Lista de entidades de régimen especial y cómo detectarlas

Casi siempre se reconocen por el `[nombre de la entidad]`. Patrones de texto que el programador puede usar como reglas de coincidencia (mayúsculas/minúsculas indiferentes):

| Tipo de entidad | Norma/régimen | Patrones en el nombre base |
|---|---|---|
| Universidades públicas e IES | Ley 30/1992 (autonomía) | "UNIVERSIDAD", "UNAL", "INSTITUCIÓN UNIVERSITARIA", "POLITÉCNICO", "COLEGIO MAYOR", "ESCUELA SUPERIOR", "ITFIP", "UNIPAMPLONA", "UPTC" |
| Empresas Sociales del Estado (hospitales) | Ley 100/1993 | "ESE", "E.S.E.", "EMPRESA SOCIAL DEL ESTADO", "HOSPITAL", "IMSALUD", "CLÍNICA" pública |
| Servicios públicos domiciliarios | Ley 142/1994 | "ESP", "E.S.P.", "EMPRESA DE SERVICIOS PÚBLICOS", "ACUEDUCTO", "AGUAS DE", "ELECTRIFICADORA", "EPM", "EMPRESAS PÚBLICAS DE", "TRIPLE A", "AIR-E", "AFINIA" |
| Empresas Industriales y Comerciales del Estado (en competencia) | Art. 14 Ley 1150/2007 | "EICE", "LOTERÍA", "LICORERA", "INDUSTRIA LICORERA", "BENEFICENCIA", "EMPRESA INDUSTRIAL Y COMERCIAL" |
| Sociedades de Economía Mixta (>50%, en competencia) | Art. 14 Ley 1150/2007 | sociedades mixtas en mercados regulados |
| Entidades financieras estatales | Art. 15 Ley 1150/2007 | "FINDETER", "BANCÓLDEX", "BANCO AGRARIO", "FINAGRO", "FONDO NACIONAL DE GARANTÍAS", "FNG", "FOGAFÍN", "ICETEX", "ENTERRITORIO", "FONADE", "FIDUCIARIA", "POSITIVA", "PREVISORA" |
| Banco de la República | Ley 31/1992 | "BANCO DE LA REPÚBLICA" |
| Sector defensa (exceptuado) | Art. 16 Ley 1150/2007 | ciertas adquisiciones de FFMM / Policía / Agencia Logística |
| Ciencia y tecnología | Ley 1286/2009 | contratos de CTeI; "MINCIENCIAS", institutos de investigación |
| Contratación con ESAL | Decreto 092/2017 | `[Tipo de contrato] = "Decreto 092 de 2017"` |
| Indígenas / educación propia (SEIP) | Decreto 1953/2014 | "CABILDO", "RESGUARDO", "INDÍGENA", "EDUCACIÓN PROPIA", "INTERCULTURAL" (ej. CEPBIN) |
| Otras de régimen propio | normas propias | "RTVC", "COLJUEGOS", "CÁMARA DE COMERCIO", "ECOPETROL", "SATENA", "SERVICIOS POSTALES", "4-72" |

**Confirmación técnica:** además del nombre, casi todas traen `[Tipo de proceso] = "Contratación régimen especial"`. Si además `[¿Uso del módulo de forma publicitaria?] = Sí`, es del subtipo a descartar siempre.

---

## 4. Los cinco tipos de no-oportunidad (con señal exacta)

| Tipo | Qué es | Señal exacta que lo delata | Qué hacer | Caso |
|---|---|---|---|---|
| 1. RFI / sondeo de mercado | No es para ofertar | `[Tipo de proceso]="Solicitud de información a los proveedores"`; cronograma "fecha límite para responder" | Vigilar (aviso temprano) | Planeación Bogotá, UNIPEP |
| 2. Cerrado al día de hoy | Estaba abierto al consultarlo; ya pasó | Hito "Presentación de Ofertas" anterior a hoy; `[Lista de respuesta de proveedores]` publicada | Descartar (todos los Ley 80 vistos) | — |
| 3. Régimen especial publicitario adjudicado | Dice "Publicado" pero ya tiene proveedor | `[Publicitario]=Sí` + `[Proveedor adjudicado]` lleno + docs "contrato/acta de inicio/propuesta ganadora" | Descartar | IMSALUD, U. Cartagena |
| 4. Ley 80 adjudicado | Terminado con ganador | `[Estado]="adjudicado y celebrado"`; `[Información de la selección]` con adjudicatario y valor | Descartar | Agencia Logística, Sativanorte |
| 5. Cancelado / desierto | Sin adjudicar | Franja "Solicitud cancelada"; `[Estado]="Proceso cancelado"`; resolución "declara desierto" | Vigilar reapertura | Santa Marta |

Las dos señales tempranas: RFI (1) y desierto (5) no son basura. Significan "esta entidad tiene una necesidad viva y probablemente sacará/repetirá un proceso real". Mandar a una bandeja "vigilar", con la entidad, el objeto y el monto estimado.

**Señal de cierre adicional y sutil:** la presencia de un documento "Informe de evaluación" indica proceso resuelto o casi, aunque el `[Estado]` siga en "Publicado" y el cronograma muestre fechas futuras. La evidencia documental manda sobre el estado y el cronograma. (U. Nacional.)

---

## 5. Problemas de datos y validación cruzada

**Principio rector:** ningún campo de SECOP es confiable solo; la verdad sale de cruzar varios.

| Problema | Detección | Solución |
|---|---|---|
| UNSPSC no capturado aunque exista | API/página traen `[Código UNSPSC]` pero el scraper lo dejó vacío | Leerlo siempre; "sin código" solo si de verdad falta. (Tránsito Atlántico tenía 78111800 y la ficha decía "sin código".) |
| UNSPSC mal puesto | El código no concuerda con el objeto | Cruzar código vs. texto del objeto; si chocan, priorizar el objeto y bajar confianza. (Sativanorte: código "ropa de seguridad" para vallas y sonido.) |
| Varios montos | `[Precio estimado total]` vs `[Valor total estimado de adquisiciones (PAA)]` vs `[Límite de contratación]` | Tomar el del contrato / valor adjudicado; nunca el del PAA. (IMSALUD: $67,5M real vs $1.000M del PAA.) |
| Lista de precios con relleno ($1) | Ítems con precio unitario 1,00 | No sacar presupuesto de la lista; usar valor oficial; si falta, sumar ítems solo como respaldo. (Planeación: 6 fases a $1.) |
| Presupuesto repartido en varios CDP | Varias filas de CDP que suman el total | Componer el total sumando los CDP. (Artesanías: CDP 373+45+42.) |
| Fechas sucias / en desorden | Formatos como "06/01/2026", inicio anterior a la firma | Normalizar; validar que la secuencia tenga sentido; si una fecha rompe el orden, confiar en la evidencia documental. (FINDETER: inicio 08/03/2026 anterior a la firma 27/07/2026.) |
| Publicación retroactiva | Fecha de publicación reciente pero fechas de ejecución y año del número de proceso son viejos | Comparar publicación vs ejecución vs año embebido en `[Número del proceso]`; si ya se ejecutó, descartar. (U. Cartagena: publicado 2026, ejecutado 2024.) |
| Registro incoherente | Título, adjudicatario, objeto, UNSPSC y documentos apuntan a cosas distintas | Marcar "datos inconsistentes, revisar manual" y bajar toda la confianza. (U. Cartagena: título FUNPROBIDES, docs de TRL SAS, objeto entre alimentación/agro/transporte.) |
| Campos de unidad corruptos | "12 (Desorden)", "7 (Desorden)" | Tolerar sin romperse; no mostrar tal cual |
| Match solo por palabra clave | Sin UNSPSC, solo coincidió una palabra del objeto | Confianza BAJA, etiquetar "revisar pertinencia". (CEPBIN.) |

---

## 6. El cronograma por modalidad

El cronograma es guía de estado, pero no es igual en todas las modalidades. La secuencia y la cantidad de hitos cambian. Lo común a las modalidades Ley 80 es que existe el hito clave "Presentación de Ofertas" (= fecha de cierre). El régimen especial casi nunca lo trae. Recordatorio: los nombres de los hitos son estables, pero las fechas vienen sucias (sección 5).

### 6.1 Mínima cuantía (Ley 80) — secuencia corta, ~3–7 días

Cronograma observado (Agencia Logística FFMM, Santa Marta):

| Orden | Hito | Si ya pasó, indica |
|---|---|---|
| 1 | Publicación de la invitación | Publicado |
| 2 | Publicación de estudios previos | Publicado |
| 3 | Plazo para recepción de observaciones | Etapa previa |
| 4 | Manifestación de interés de limitar a MiPymes | Etapa previa |
| 5 | Respuesta a las observaciones | Etapa previa |
| 6 | Aviso de limitación a MiPymes (o no) | Etapa previa |
| 7 | Plazo máximo para expedir adendas | Cerca del cierre |
| 8 | PRESENTACIÓN DE OFERTAS | Fecha de cierre |
| 9 | Apertura de sobres | Cerrado para ofertar |
| 10 | Informe de presentación de ofertas | Cerrado, en revisión |
| 11 | Publicación del informe de evaluación | En evaluación |
| 12 | Observaciones al informe de evaluación | En evaluación |
| 13 | Aceptación de ofertas | Terminado |
| 14 | Entrega y aprobación de garantías | En ejecución |

No tiene proyecto de pliego ni sorteo. El desenlace (adjudicado/desierto) se ve en `[Información de la selección]`.

### 6.2 Selección abreviada de menor cuantía (Ley 80) — secuencia larga, ~10–20 días

Cronograma observado (Instituto de Tránsito del Atlántico, Sativanorte). Añade etapa de proyecto de pliego, sorteo y precalificación que la mínima cuantía no tiene:

| Orden | Hito | Si ya pasó, indica |
|---|---|---|
| 1 | Aviso de convocatoria pública | Publicado |
| 2 | Publicación de estudios previos | Publicado |
| 3 | Proyecto de pliego de condiciones | Publicado |
| 4 | Observaciones al proyecto de pliego | Etapa previa |
| 5 | Manifestación de interés de limitar a MiPymes | Etapa previa |
| 6 | Respuesta a las observaciones | Etapa previa |
| 7 | Pliego de condiciones definitivo | Etapa previa |
| 8 | Acto administrativo de apertura | Abierto |
| 9 | Manifestación de interés / sorteo / lista de precalificados | Abierto |
| 10 | Observaciones al pliego definitivo | Abierto |
| 11 | Plazo máximo para adendas | Cerca del cierre |
| 12 | PRESENTACIÓN DE OFERTAS | Fecha de cierre |
| 13 | Apertura de ofertas | Cerrado para ofertar |
| 14 | Informe de presentación de ofertas | Cerrado, en revisión |
| 15 | Publicación del informe de evaluación | En evaluación |
| 16 | Observaciones al informe de evaluación | En evaluación |
| 17 | Adjudicación o Declaración de Desierto | Terminado (ver desenlace) |
| 18 | Firma del contrato | Adjudicado y celebrado |
| 19 | Entrega y aprobación de garantías | En ejecución |

### 6.3 Licitación pública y concurso de méritos (Ley 80)

No hubo casos en la muestra, pero la estructura es la misma de la selección abreviada (proyecto de pliego → pliego definitivo → ofertas → evaluación → adjudicación), con ventanas más largas. El hito de cierre sigue siendo "Presentación de Ofertas". El concurso de méritos añade apertura de sobres técnico y económico por separado.

### 6.4 Régimen especial — sin hitos de selección

Cronograma observado (IMSALUD, Artesanías, U. Nacional, FINDETER): normalmente solo trae:

| Hito | Nota |
|---|---|
| Fecha de publicación del proceso | — |
| Fecha límite de presentación de ofertas | Solo en el subtipo "con ofertas"; muchas veces ausente |
| Fecha de firma del contrato | Si ya pasó y hay adjudicatario → cerrado |
| Fecha de inicio de ejecución | Si ya pasó → en ejecución |
| Plazo de ejecución | Fin del contrato |

**Clave:** si el cronograma no trae un hito de "presentación de ofertas", es señal de régimen especial → la fecha de cierre (si el proceso es ofertable) hay que buscarla en el PDF (requerimientos mínimos / invitación / adenda). (FINDETER no mostraba fecha de ofertas en el cronograma.)

### 6.5 RFI (Solicitud de información) — no hay cierre de ofertas

| Hito | Nota |
|---|---|
| Fecha de publicación | — |
| Plazo para solicitar aclaraciones | — |
| Fecha límite para responder la solicitud de información | No es un cierre de ofertas; es plazo para enviar info |

No se adjudica. (Planeación Bogotá, UNIPEP.)

### 6.6 Reglas de uso (todas las modalidades)

- **Fecha de cierre Ley 80:** tomar el hito "Presentación de Ofertas" del cronograma. No hace falta PDF.
- **Estado por posición:** antes del hito de ofertas → ABIERTO (días contra la fecha de consulta); entre apertura e informe de evaluación → en evaluación; adjudicación/firma en adelante → terminado.
- **Desenlace:** cruzar con `[Información de la selección]` — con adjudicatario = adjudicado (tipo 4); sin él + resolución de desierto = desierto (tipo 5).
- **Sin hito de ofertas** → régimen especial: ir al PDF (o descartar según sección 3).
- **Siempre:** normalizar fechas y validar el orden; si una fecha rompe la secuencia, confiar en la evidencia documental (lista de oferentes, informe de evaluación, contrato) antes que en el cronograma.

---

## 7. Requisitos habilitantes: ¿puedo participar de verdad?

Aunque el tema coincida y el proceso esté abierto, puede haber barreras legales que descalifican. Viven dentro del pliego / cuestionario (sección "REQUISITOS HABILITANTES"). El sistema debe extraerlas y compararlas contra el perfil de la empresa.

| Requisito | Qué buscar en el texto | Por qué importa |
|---|---|---|
| Habilitación sectorial | "habilitación", "resolución del Ministerio de…", "Decreto 431 de 2017" (transporte especial) | Descalifica a quien no sea del sector. (Tránsito Atlántico: solo transportadores habilitados.) |
| RUP | "Registro Único de Proponentes", "RUP vigente" | Excluye a quien no esté inscrito con la especialidad |
| Experiencia mínima | "experiencia", "X% del presupuesto", "1,5 veces el valor", "contratos de objeto igual" | Filtra por tamaño/trayectoria; favorece al incumbente. (Artesanías: experiencia por 1,5× = $758,7M.) |
| Capacidad financiera | "estados financieros", "indicadores", "ANEXO 6" | Excluye a empresas pequeñas o nuevas |
| SG-SST / ambiental / RAE | "Resolución 0312 de 2019", "gestión ambiental", "recolección de residuos" | Suma costo de preparación de la oferta |
| Antigüedad mínima | "constituida mínimo X años antes" | Excluye empresas recientes |

**Uso:** marcar cada lead con los habilitantes detectados y un semáforo "¿la empresa los cumple?". Si hay una barrera dura que la empresa no cumple (ej. habilitación sectorial), bajar el lead aunque esté abierto.

---

## 8. Señales de competencia (entre "abierto real" y "amarrado")

No todo se reduce a abierto/cerrado; hay grados de disputa que afectan cuánto vale la pena ir.

| Señal | Cómo detectarla | Lectura |
|---|---|---|
| Amarrado (dirigido) | Producto/marca con nombre propio en el objeto + un solo ítem por el valor exacto | Casi imposible de ganar. (Camila Molano: "Colegios Online".) |
| Falso positivo de "amarrado" | Marcas de insumos compatibles en lista de varios ítems | NO es dirigido: cualquiera consigue los insumos. (Agencia Logística: tóner HP/Samsung.) |
| Abierto en el papel | Oferente único + objeto vago sin producto definido + oferta igual al presupuesto | Competido en teoría, definido en la práctica. (Artesanías: un oferente, objeto vago, oferta = techo exacto.) |
| Baja competencia / proveedor local | Oferente único + del mismo municipio pequeño + persona natural | No es amarrado, pero competir desde afuera es cuesta arriba. (Sativanorte.) |

**Regla para no equivocarse:** la señal fuerte de amarrado es la marca de un producto único en el objeto; el "un ítem por el valor exacto" es solo apoyo y por sí solo da falsos positivos.

---

## 9. Puntaje: que el número sirva

Hoy procesos muy distintos reciben el mismo puntaje (varios dieron 42/100), señal de que no discrimina — probablemente porque el UNSPSC perdido fuerza siempre el mismo castigo. El puntaje debería combinar, con pesos configurables:

1. **Confianza del match** (alta si UNSPSC coincide con el objeto; baja si es solo palabra clave).
2. **Ofertabilidad** (RFI o régimen especial publicitario → puntaje mínimo).
3. **Estado al día de la consulta** (cerrado/adjudicado/desierto → fuera).
4. **Días restantes** (más días = más accionable).
5. **Viabilidad** (penaliza barreras habilitantes que la empresa no cumple).
6. **Competencia** (penaliza amarrado / baja competencia).
7. **Relación valor/esfuerzo** (monto del contrato vs. cantidad de habilitantes a reunir).

**Validación:** un proceso como FINDETER (vivo, ofertable, buen monto, sin barreras extremas) debe salir alto; un RFI o un adjudicado deben salir al fondo. Si dan parecido, el puntaje está mal.

---

## 10. Esquema mínimo de datos a extraer

| Campo | Fuente | Uso |
|---|---|---|
| `notice_uid` | URL (`noticeUID=`) | Identificador |
| `numero_proceso` | `[Número del proceso]` | Detecta año retroactivo |
| `entidad` | `[Entidad]` | Detecta régimen especial por patrón |
| `objeto` / `descripcion` | `[Título]`/`[Descripción]` | Match + validación cruzada |
| `tipo_proceso` | `[Tipo de proceso]` | Reemplaza el "Otro"; clasifica el mundo |
| `es_publicitario` | `[¿Uso del módulo publicitario?]` | Subtipo de régimen especial |
| `unspsc` | `[Código UNSPSC]` + lista adicional | Match por código |
| `presupuesto` | `[Precio estimado total]` / valor adjudicado | Nunca del PAA |
| `fecha_publicacion` | Cronograma | Detecta retroactivo |
| `fecha_cierre` | Hito "Presentación de Ofertas" o PDF | Calcular días |
| `proveedor_adjudicado` | `[Proveedor adjudicado]` / `[Información de la selección]` | Detecta adjudicado |
| `estado_secop` | `[Estado]` | Pista (no confiar a ciegas) |
| `documentos` | `[Documentación]` | Señales: contrato, acta, informe de evaluación |
| `habilitantes` | Cuestionario / pliego (PDF) | Viabilidad |

---

## 11. El embudo de filtros, en orden

1. **¿Es ofertable?** → descartar RFI (tipo 1).
2. **¿Es régimen especial?** → descartar (publicitario siempre; con ofertas, revisar).
3. **¿Es un registro retroactivo o incoherente?** → descartar / marcar revisión manual.
4. **¿Sigue abierto al día de la consulta?** → descartar cerrados, adjudicados, desiertos (2–5).
5. **¿Está amarrado o con baja competencia?** → bajar puntaje (sección 8).

---

## Notas para la iteración con el código fuente

*(Espacio para comparar contra lo ya implementado y anotar brechas/filtros pendientes)*

- [ ] ¿Ya extraemos `[Tipo de proceso]` literal en vez de mostrar "Otro"?
- [ ] ¿Está implementada la categorización REGIMEN_ESPECIAL / RFI / LEY_80?
- [ ] ¿Tenemos el interruptor configurable de descarte de régimen especial (todo vs. solo publicitario)?
- [ ] ¿Detectamos entidades de régimen especial por patrones de nombre (sección 3.3)?
- [ ] ¿Implementamos los 5 tipos de no-oportunidad con sus señales exactas?
- [ ] ¿Hacemos validación cruzada de montos (nunca tomar del PAA)?
- [ ] ¿Normalizamos fechas y detectamos publicación retroactiva?
- [ ] ¿El cronograma se procesa distinto según modalidad (mínima cuantía vs. selección abreviada vs. régimen especial vs. RFI)?
- [ ] ¿Extraemos requisitos habilitantes y los cruzamos con perfil de empresa?
- [ ] ¿Detectamos señales de "amarrado" vs. falsos positivos?
- [ ] ¿El puntaje combina los 7 factores de la sección 9 con pesos configurables?
