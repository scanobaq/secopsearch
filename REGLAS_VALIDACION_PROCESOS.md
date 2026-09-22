# Reglas actuales de validación, puntuación y alertas de procesos SECOP

Este documento describe el comportamiento **AS-IS** del código: cómo un registro de SECOP II se obtiene, se filtra, se convierte en candidato, se persiste, se evalúa para cada proveedor, se clasifica y puede terminar en un intento real de envío por Telegram. Cuando un comentario, una prueba, una configuración o una intención histórica contradicen el código ejecutable, este documento toma como autoridad el código actual.

> **Alcance temporal:** estado observado en el repositorio al 13 de agosto de 2026. No es una propuesta de reglas futuras.

## Vista rápida

```text
Ventana de fechas
    |
    +--> Consulta SECOP filtrada por UNSPSC exacto/clase
    |        |
    |        +--> validez --> apertura/fase/fecha --> descartar RFI y régimen especial
    |
    +--> Consulta SECOP general
             |
             +--> mismos filtros --> coincidencia con alguna palabra clave global
                              |
                              v
             Asociación proceso-proveedor elegible
             (proveedor con embedding + UNSPSC o palabra clave según la fuente)
                              |
                              v
             Candidato único por ID de proceso
                              |
                    ¿ya existe en la base?
                       | sí             | no
                       v                v
                 omitir todo      mapear + generar embedding
                                  + persistir proceso
                                           |
                              similitud por proveedor asociado
                                           |
                              ¿similitud >= 0.40?
                                | no              | sí
                                v                 v
                           sin puntaje       scoring + etiqueta
                                                  |
                                          persistir puntaje
                                         (proceso, proveedor)
                                                  |
                              ¿total >= 40 y proveedor tiene chat?
                                | no              | sí
                                v                 v
                              nada       solo log de alerta simulada
```

La sincronización **sí persiste el proceso antes de conocer su similitud**. Por eso un proceso almacenado no implica que exista un puntaje, y un puntaje tampoco implica que haya existido un mensaje real de Telegram.

## Conceptos que no deben confundirse

| Concepto | Qué significa actualmente | Qué no garantiza |
|---|---|---|
| Registro obtenido | DTO devuelto por alguna consulta a SECOP II | Que sea válido, aplicable o relevante |
| Candidato | Proceso que superó los filtros globales y quedó asociado al menos a un proveedor con embedding por UNSPSC o palabra clave | Que alcance el umbral semántico |
| Proceso almacenado | Fila única en `procesos`, creada antes del scoring | Que tenga puntaje o alerta |
| Puntaje almacenado | Evaluación de un par `(proceso, proveedor)` que algún flujo decidió persistir | Que sea un proceso único, una alerta o un mensaje enviado |
| Etiqueta | Clasificación `Descartar`, `Analizar` o `Proponer` derivada del total | Que Telegram haya recibido algo |
| Alerta potencial en sincronización | Puntaje `>= 40` y proveedor con `TelegramChatId` | Envío real: ese bloque solo escribe un log simulado |
| Intento real de alerta | Llamada a `IAlertaService.EnviarAlertaProcesoAsync` desde `EnviarAlertasHandler` | Entrega confirmada ni registro en la tabla `alertas` |

## Umbrales y decisiones principales

| Decisión | Regla ejecutable actual | Resultado |
|---|---|---|
| DTO válido | ID, título y nombre de entidad no vacíos | Puede continuar |
| Aplicable | Apertura `Abierto`, estado `Publicado` y reglas de fase/fecha descritas más adelante | Puede continuar |
| Régimen admitido | Clasificación distinta de `Rfi` y `RegimenEspecial` | Solo `Ley80` continúa en sincronización |
| Candidato por proveedor | Proveedor con embedding y coincidencia UNSPSC o palabra clave según la fuente | El proveedor se asocia al candidato |
| Persistencia del proceso | Candidato nuevo cuyo ID no existe en `procesos` | Se genera embedding y se guarda antes del scoring |
| Scoring en sincronización | Similitud coseno `>= 0.40` | Se calcula y persiste un puntaje por proveedor |
| Scoring en recálculo | Similitud coseno `>= 0.40` | Se calcula y persiste un puntaje por proveedor |
| Etiqueta `Descartar` | Total `< 40` o evaluación inhabilitada | No es seleccionada por `EnviarAlertasHandler` |
| Etiqueta `Analizar` | `40 <= total < 70` | Es elegible para alerta real diferida |
| Etiqueta `Proponer` | Total `>= 70` | Es elegible para alerta real diferida |
| Alerta potencial durante sincronización | Total `>= 40` y chat configurado | Solo log; el envío está comentado |
| Alerta real diferida | Puntaje no `Descartar`, no inhabilitado, proceso vigente y chat configurado | Se intenta enviar mediante Telegram |

## 1. Obtención desde SECOP II

### 1.1 Ventana temporal

`SincronizarProcesosHandler` realiza en paralelo dos llamadas a `ISecopApiClient.ObtenerProcesosRecientesAsync` usando la misma ventana solicitada:

1. Una consulta filtrada por los códigos UNSPSC de todos los proveedores y sus clases de seis dígitos.
2. Una consulta general, sin filtro UNSPSC, usada para encontrar coincidencias por palabras clave.

`SecopApiClient` consulta el dataset `p6dx-8zbt` y aplica estas comparaciones sobre `fecha_de_ultima_publicaci`:

| Parámetros | Predicado temporal |
|---|---|
| Solo `desde` | `fecha_de_ultima_publicaci >= desde` |
| `desde` y `hasta` | `fecha_de_ultima_publicaci >= desde AND fecha_de_ultima_publicaci < hasta` |

Cada página contiene hasta 1000 registros, ordenados por `fecha_de_ultima_publicaci DESC, id_del_proceso DESC`. Los códigos se dividen en lotes de 40 para evitar URLs demasiado largas. Los resultados de los lotes se deduplican por `Id`, conservando el primer DTO de cada grupo.

Si una llamada HTTP, la deserialización u otra operación dentro de `EjecutarConsultaAsync` falla, el cliente registra el error y devuelve una lista vacía para esa consulta; no propaga la excepción.

### 1.2 Alcance del filtro UNSPSC remoto

La consulta filtrada busca exclusivamente en `codigo_principal_de_categoria`:

- Coincidencia exacta: `V1.` más el código de ocho dígitos del proveedor.
- Coincidencia por clase: prefijo `V1.` más los primeros seis dígitos.
- `categorias_adicionales` **no participa** en la consulta remota ni en la asociación local del candidato.

Cada código del proveedor se pasa por `CodigoUnspsc` al derivar la clase. Debe contener exactamente ocho dígitos después de `Trim`; un código vacío, alfanumérico o de otra longitud lanza una excepción y puede abortar la sincronización completa.

Si la colección global de códigos queda vacía, `SecopApiClient` no genera predicados UNSPSC y esa primera llamada se convierte en otra consulta general por fecha. El handler sigue marcando sus resultados como procedentes de la fuente UNSPSC; una coincidencia posterior por palabra clave puede entonces quedar sin la advertencia `EncontradoPorTexto` aunque no hubo filtro UNSPSC remoto.

### 1.3 Activación manual

`POST /api/procesos/sincronizar` es la única entrada de sincronización de procesos. Usa `desde`/`hasta` sobre la fecha de última publicación; si faltan, toma las últimas `horasAtras` (2 por defecto) hasta la hora actual.

## 2. Filtros globales por proceso

Estos filtros se aplican al DTO independientemente de un proveedor particular. Que un proceso los supere todavía no significa que vaya a persistirse: después debe quedar asociado a un proveedor elegible.

### 2.1 Validez mínima: `EsValido`

Un DTO es válido únicamente si los tres campos siguientes contienen texto no blanco:

- `Id` (`id_del_proceso`).
- `Titulo` (`nombre_del_procedimiento`).
- `NombreEntidad` (`entidad`).

No se exige objeto, presupuesto, fecha de cierre, URL, modalidad ni código UNSPSC para esta validación mínima.

### 2.2 Apertura, estado, fase y fecha: `EstaAbiertoParaAplicar`

Primero deben cumplirse simultáneamente:

- `EstadoApertura` es exactamente `Abierto`, sin distinguir mayúsculas/minúsculas.
- `Estado` es exactamente `Publicado`, sin distinguir mayúsculas/minúsculas.

Estas comparaciones no hacen `Trim`; espacios adicionales provocan rechazo.

Después existen dos caminos:

| Camino | Fase/resumen requerido | Regla de fecha de cierre |
|---|---|---|
| Fase conocida | `Fase` **o** `EstadoResumen` es `Presentación de oferta` o `Fase de ofertas` | Si la fecha no existe o no se puede parsear, se acepta. Si se parsea, su fecha calendario debe ser hoy UTC o posterior |
| Fallback sin fase | `Fase` vacío y `EstadoResumen` vacío o `No Definido` después de `Trim` | La fecha debe ser un timestamp ISO explícitamente parseable y representar un instante igual o posterior a `DateTimeOffset.UtcNow` |

Consecuencias AS-IS:

- En el camino de fase conocida, una fecha ausente o inválida **no bloquea** el proceso.
- En ese mismo camino se compara solo `.Date`; un cierre ocurrido horas antes durante el día UTC todavía puede pasar el filtro.
- El fallback es más estricto: rechaza fecha ausente, inválida, ambigua o ya vencida y compara el instante completo.
- Un valor de fase distinto de los dos aceptados no usa el fallback, aunque la fecha sea futura.
- Las dos fases conocidas también se comparan sin `Trim`; espacios adicionales provocan rechazo.

### 2.3 Clasificación del régimen y descarte

`ObtenerClasificacion` convierte la modalidad a mayúsculas y evalúa en este orden:

| Contenido de `modalidad_de_contratacion` | Clasificación |
|---|---|
| Contiene `SOLICITUD DE INFORMACI` | `Rfi` |
| Contiene `GIMEN ESPECIAL` | `RegimenEspecial` |
| Cualquier otro valor, incluido nulo | `Ley80` |

`SincronizarProcesosHandler.DebeDescartarse` elimina siempre `Rfi` y `RegimenEspecial`. Esto incluye `Régimen Especial (con ofertas)`. No existe una excepción activa ni un interruptor de configuración: solo `Ley80` continúa.

El nombre de la entidad se analiza en otra etapa y **no cambia esta clasificación**. Una entidad cuyo nombre parece de régimen especial solamente agrega una advertencia al puntaje si el proceso ya fue admitido como `Ley80`.

La modalidad persistida se obtiene por coincidencia de subcadena sobre el mismo texto en mayúsculas:

| Subcadena | `ModalidadContrato` |
|---|---|
| `LICITACI` | `LicitacionPublica` |
| `ABREVIADA` | `SeleccionAbreviada` |
| `RITOS` | `ConcursoMeritos` |
| `DIRECTA` | `ContratacionDirecta` |
| `NIMA CUANT` | `MinimaCuantia` |
| `ACUERDO` | `AcuerdoMarcoPrecios` |
| Ninguna de las anteriores | `Otro` |

Este mapeo se guarda y se muestra en Telegram, pero no modifica el descarte: esa decisión usa `ObtenerClasificacion`, no `ObtenerModalidad`.

## 3. Evaluación por proveedor y formación de candidatos

Una vez superados los filtros globales, el proceso debe asociarse al menos a un proveedor. Aquí comienzan las reglas específicas por proveedor.

### 3.1 Requisito previo: embedding del proveedor

Un proveedor sin `Embedding` se omite por completo, aunque coincidan sus códigos UNSPSC o sus palabras clave. Si ningún proveedor con embedding queda asociado, no se crea candidato y el proceso no se persiste.

El embedding del proveedor se genera con `ConstructorTextoSemantico.CrearParaProveedor` usando solo:

1. `ExperienciaDescripcion`, bajo la etiqueta `Experiencia y capacidades`.
2. `PalabrasClave`, bajo la etiqueta `Palabras clave de búsqueda`.

Los valores vacíos se eliminan, los espacios internos se normalizan y los duplicados dentro de cada sección se eliminan sin distinguir mayúsculas/minúsculas. No se incluyen nombre, NIT, capacidad financiera ni códigos UNSPSC.

### 3.2 Coincidencia UNSPSC local

Los códigos del proceso y del proveedor aceptan estos formatos antes de compararse:

- `V1.12345678`.
- `V112345678`.
- `12345678`.

Después de quitar el prefijo deben quedar exactamente ocho dígitos. La coincidencia es verdadera cuando:

- Los ocho dígitos son iguales; o
- Los primeros seis dígitos, correspondientes a la clase, son iguales.

Solo se compara `CodigoPrincipalCategoria`; las categorías adicionales persistidas no se usan para decidir el candidato.

### 3.3 Coincidencia por palabras clave

Las palabras clave se buscan únicamente en `Objeto`, no en `Titulo`.

| Longitud de la palabra clave después de `Trim` | Comparación |
|---|---|
| 1 a 3 caracteres | Expresión regular con límites respecto de letras y números; evita que `IA` coincida dentro de `social` |
| Más de 3 caracteres | Subcadena con `Contains`, sin distinguir mayúsculas/minúsculas |

No hay normalización de acentos, lematización, stemming ni tokenización para palabras de más de tres caracteres. Por ejemplo, una palabra larga puede coincidir dentro de otra palabra porque se usa una subcadena simple.

### 3.4 Reglas según la fuente

| Fuente del DTO | Asociación requerida para cada proveedor con embedding |
|---|---|
| Consulta marcada como UNSPSC | Coincidencia con código UNSPSC **o** con alguna palabra clave propia del proveedor |
| Consulta general de palabras clave | Coincidencia con alguna palabra clave propia del proveedor |

Antes de la segunda ruta, la consulta general se reduce a DTO válidos, aplicables, no descartados y cuyo objeto coincida con al menos una palabra clave de cualquier proveedor. Esa preselección global no basta: luego se vuelve a exigir la coincidencia con las palabras del proveedor específico.

Los candidatos se deduplican por ID de proceso y acumulan un conjunto de proveedores. Un mismo proceso se mapea, embebe y persiste una sola vez, pero puede producir varios puntajes, uno por proveedor asociado.

### 3.5 Gotcha de procedencia `keyword-only`

`EsKeywordOnly` pertenece al candidato completo, no a cada asociación proceso-proveedor, y queda fijado cuando se crea el candidato:

- Si el ID aparece primero en la fuente UNSPSC, queda `false`, aunque un proveedor concreto haya entrado solo por palabra clave.
- Si aparece primero en la fuente general, queda `true` y el puntaje recibe la advertencia `Encontrado por coincidencia de palabra clave (sin código UNSPSC declarado)`.
- Si el mismo ID aparece después en la otra fuente, se agregan proveedores pero no se cambia esa bandera.

Por lo tanto, la advertencia de procedencia puede no representar con precisión el motivo de asociación de cada proveedor.

## 4. Mapeo, embedding y persistencia del proceso

### 4.1 Duplicados existentes

Antes de mapear, el handler consulta `IProcesoRepository.ExisteAsync(dto.Id)`. Si el proceso ya existe:

- No actualiza sus datos.
- No regenera su embedding.
- No evalúa proveedores nuevos.
- No recalcula puntajes.
- No produce siquiera el log de alerta potencial.

La sincronización es, por tanto, un flujo de inserción para IDs nuevos, no un refresco de procesos existentes.

### 4.2 Conversión de datos

Para un candidato nuevo:

- Presupuesto: `decimal.TryParse`; si falla, queda `0`.
- Fecha de cierre: `DateTime.TryParse`; si falla, queda `DateTime.MinValue` marcado como UTC.
- Fecha de publicación: mismo comportamiento.
- Valor y fecha de adjudicación: quedan `null` si no se pueden parsear.
- Contadores de competencia: quedan `null` si no se pueden parsear como entero.
- Campos textuales nulos requeridos por el constructor se convierten a cadena vacía.

La detección de estado adjudicado tiene precedencia cuando `Adjudicado` es `Si`/`true`, o cuando existen simultáneamente nombre del proveedor adjudicado y fecha de adjudicación. En los demás casos se buscan, en orden lógico, textos como `DESIERTO`, `CANCELADO`, `SUSPENDIDO`, `CERRADO` y `SELECCIONADO`; cualquier otro valor se mapea como `Activo`.

Como el filtro de aplicabilidad exige que `Estado` sea `Publicado` pero no consulta los campos de adjudicación, un DTO puede superar el filtro como publicado y después mapearse como `Adjudicado`. La sincronización inicial aun puede calcularle puntaje; en cambio, el recálculo posterior no lo seleccionará porque `ObtenerActivosAsync` exige estado `Activo`.

### 4.3 Texto semántico del proceso

`ConstructorTextoSemantico.CrearParaProceso` concatena:

```text
<Título normalizado> <Objeto normalizado>
```

Omite partes vacías y normaliza secuencias de espacios. No incluye entidad, presupuesto, modalidad, UNSPSC ni otros metadatos.

`OpenAiEmbeddingService` usa el modelo codificado directamente como `text-embedding-3-small`. El valor `OpenAI:EmbeddingModel` de configuración no controla actualmente esa constante.

### 4.4 Orden de persistencia

El orden ejecutable es:

1. Mapear `Proceso`.
2. Generar embedding.
3. Asignar embedding.
4. Guardar la fila en `procesos`.
5. Calcular similitud y, si corresponde, puntajes por proveedor.

Un fallo después del paso 4 puede dejar el proceso almacenado sin todos sus puntajes. El contador devuelto por la sincronización (`nuevos`) cuenta procesos guardados, no pares evaluados ni alertas.

## 5. Umbral semántico

La similitud usada es el coseno:

```text
similitud = dot(proceso, proveedor) / (|proceso| * |proveedor|)
```

Si alguno de los vectores tiene magnitud cero, el servicio devuelve `0`.

### Umbrales por flujo

| Flujo | Umbral inclusivo | Persistencia del puntaje | Otros filtros relevantes |
|---|---:|---|---|
| `SincronizarProcesosHandler` | `0.40` | Sí | Solo candidatos nuevos y proveedores previamente asociados por UNSPSC/palabra clave |
| `RecalcularPuntajesHandler` | `0.40` | Sí | Procesos con estado `Activo`, vigentes y con embedding; proveedores con embedding |
| `BuscarProcesosHandler` | `0.40` | No | Búsqueda pgvector, proceso vigente y etiqueta distinta de `Descartar` |
| `ObtenerDetalleHandler` | `0.40` | No | Elige el proveedor de mayor similitud; no exige vigencia ni elimina etiqueta `Descartar` |
| `CalcularPuntajeHandler` | `0.40` | Sí | Solo exige que proceso, proveedor y ambos embeddings existan |

La condición se implementa como `if (similitud < umbral) continue`, por lo que el valor exactamente igual al umbral se acepta.

El recálculo y la búsqueda no vuelven a exigir coincidencia UNSPSC ni palabra clave: comparan semánticamente los procesos almacenados con los proveedores objetivo.

## 6. Fórmula exacta del puntaje actual

### 6.1 Regla inhabilitante activa

Antes de sumar componentes, `ScoringService` evalúa `proceso.EsSoloEsal()`. Un tipo de contrato que contenga simultáneamente `092` y `2017` se considera exclusivo para ESAL.

Si se detecta:

- Total: `0`.
- Todos los componentes: `0`.
- Etiqueta: `Descartar`.
- `EsInhabilitado`: `true`.
- Advertencia: `Solo aplica a ESAL (Decreto 092 de 2017) - proveedor no tiene perfil ESAL`.

No existe en `Proveedor` un atributo que habilite un perfil ESAL; por eso todo proceso detectado con esa marca se inhabilita para cualquier proveedor evaluado.

### 6.2 Componentes activos y desactivados

Para procesos no inhabilitados, la fórmula ejecutable es:

```text
puntaje_similitud  = similitud * 35
puntaje_requisitos = 15 + (cubre_capacidad_financiera ? 10 : 0)
puntaje_tiempo     = 0
puntaje_competencia = tabla_de_competencia
puntaje_entidad    = 0

total = min(
    puntaje_similitud
  + puntaje_requisitos
  + puntaje_competencia,
  100
)
```

| Componente | Máximo declarado | Cálculo activo |
|---|---:|---|
| Similitud semántica | 35 | `similitud * 35` |
| Requisitos | 25 | 15 puntos base para todo proceso no ESAL + 10 si la capacidad financiera cubre el 15% del presupuesto |
| Tiempo disponible | 20 | **Desactivado: siempre 0** |
| Competencia estimada | 12 | Activo según contadores reales |
| Historial de entidad | 8 | **Desactivado: siempre 0** |

Con similitud máxima convencional de `1`, el máximo práctico actual es `72`, no `100`: 35 de similitud + 25 de requisitos + 12 de competencia.

### 6.3 Requisitos financieros y RUP

La capacidad se considera suficiente cuando:

```text
CapacidadFinanciera >= PresupuestoProceso * 0.15
```

Si el presupuesto no pudo parsearse y quedó en `0`, cualquier capacidad financiera no negativa satisface esta condición y obtiene los 10 puntos adicionales.

La validación de RUP vencido está **comentada** dentro de `ScoringService`. En consecuencia:

- `Proveedor.RupVigente()` existe, pero no inhabilita el scoring.
- La advertencia `RupVencido` no se agrega por este flujo.
- Los 15 puntos base se otorgan aunque el RUP esté vencido.
- Los comentarios que afirman `RUP vencido -> puntaje 0` no describen el comportamiento ejecutable actual.

### 6.4 Competencia

Si los cinco contadores son `null`, el componente recibe un valor neutral de `6`.

Si existe al menos un contador, se toma el primero no nulo en este orden:

1. `ProveedoresUnicosCon`.
2. `ConteoRespuestasOfertas`.
3. `RespuestasAlProcedimiento`.
4. `ProveedoresQueManifestaron`.
5. `ProveedoresInvitados`.

| Competidores seleccionados | Puntos |
|---:|---:|
| 0 | 12 |
| 1 | 10 |
| 2 a 3 | 8 |
| 4 a 6 | 5 |
| 7 o más | 2 |

La regla no combina contadores: usa solamente el primero disponible según esa precedencia.

### 6.5 Tiempo e historial de entidad

El código conserva bloques comentados para:

- Puntuar días hábiles restantes hasta 20 puntos.
- Advertir por plazo de tres días o menos.
- Otorgar 4 puntos neutrales de historial de entidad.

Ninguno se ejecuta. `PuntajeTiempo` y `PuntajeEntidad` se construyen y persisten en `0`, y no se agrega `PlazoMuyCorto`.

## 7. Etiquetas

Para un puntaje no inhabilitado:

| Total | Etiqueta |
|---:|---|
| `>= 70` | `Proponer` |
| `>= 40` y `< 70` | `Analizar` |
| `< 40` | `Descartar` |

La etiqueta pertenece a un par proceso-proveedor. El mismo proceso puede ser `Proponer` para un proveedor, `Analizar` para otro y no tener ningún puntaje para un tercero.

Debido a que tiempo e historial están desactivados, llegar a `Proponer` exige estar muy cerca del máximo de los componentes activos. Por ejemplo, incluso con requisitos y competencia máximos, se necesita una similitud mínima aproximada de `0.942858` para alcanzar 70 puntos.

## 8. Persistencia de puntajes

La tabla `puntajes` tiene una restricción única por `(ProcesoId, ProveedorId)`. `PuntajeRepository.GuardarAsync` implementa el reemplazo así:

1. Busca un puntaje existente para el par.
2. Si existe, lo elimina y guarda los cambios.
3. Agrega un nuevo registro con un nuevo `Id` y `CalculadoEn`.

Por ello:

- Un proceso puede tener varios registros de puntaje, pero como máximo uno por proveedor.
- Recalcular un par que supera el umbral reemplaza su registro anterior; no conserva versiones múltiples del mismo par.
- Si durante el recálculo la similitud queda por debajo de `0.40`, el handler simplemente continúa y **no elimina** el puntaje previo. Esos puntajes históricos o potencialmente obsoletos permanecen tal como están.
- No existe una regla vigente de depuración automática de esos puntajes.

Las advertencias `EncontradoPorTexto` y `PosibleRegimenEspecial` se agregan en `SincronizarProcesosHandler`, no dentro de `ScoringService`. Un recálculo que reemplace el puntaje no vuelve a agregarlas.

## 9. Alertas potenciales y alertas reales

### 9.1 Bloque de sincronización: solo simulación

Después de persistir un puntaje, `SincronizarProcesosHandler` comprueba:

```text
PuntajeTotal >= 40 AND TelegramChatId tiene valor
```

La constante se llama `UmbralAlertaProponer`, pero el valor `40` incluye toda la categoría `Analizar`; el nombre es incorrecto respecto de su comportamiento.

La llamada real a `_alertas.EnviarAlertaProcesoAsync` está comentada. Si la condición se cumple, el código solo registra `Alerta Telegram simulada...`. No se envía mensaje ni se crea una fila en `alertas`.

### 9.2 `EnviarAlertasHandler`: intento real diferido

Este handler sí llama a Telegram. Para un proveedor, las condiciones son:

1. El proveedor existe y tiene `TelegramChatId`; de lo contrario termina sin hacer nada.
2. Se cargan **todos** sus puntajes almacenados.
3. Se conservan los que tienen etiqueta distinta de `Descartar` y `EsInhabilitado == false`.
4. Se carga el proceso de cada puntaje.
5. El proceso debe existir y cumplir `FechaCierre > DateTime.UtcNow`.
6. Se llama a `IAlertaService.EnviarAlertaProcesoAsync`.

No se vuelve a comprobar:

- El umbral semántico actual.
- UNSPSC o palabras clave.
- Estado `Activo`.
- Si el mismo puntaje ya fue enviado.
- Si existe una fila previa en `alertas`.

`RupAlertaWorker` ejecuta `EnviarAlertasHandler` para todos los proveedores cada 24 horas. Aunque su nombre y comentarios enfatizan el RUP, cada ejecución también intenta enviar nuevamente todos los puntajes de proceso que sigan cumpliendo las condiciones anteriores. El endpoint manual `POST /api/procesos/alertas-rup` hace lo mismo.

### 9.3 Resultado del envío y tabla `alertas`

`TelegramAlertaService` devuelve el ID del mensaje cuando Telegram responde correctamente. Si hay una excepción, registra el error y devuelve `0`.

El handler que lo invoca no persiste ese resultado. Aunque existen la entidad `Alerta`, la tabla `alertas` y el campo `TelegramMessageId`, no hay código activo en este flujo que cree el registro. Por tanto, la base de datos no ofrece actualmente una confirmación ni deduplicación de alertas de procesos enviadas.

### 9.4 Alerta de RUP separada

Después de procesar alertas de procesos, `EnviarAlertasHandler` intenta una alerta de RUP cuando:

```text
DiasParaVencimientoRup() <= 30
```

No existe límite inferior: un RUP ya vencido produce días negativos y también satisface la condición. Tampoco existe deduplicación, por lo que puede intentarse nuevamente en cada ejecución diaria. Esta alerta no cambia el puntaje porque la inhabilitación por RUP en `ScoringService` está comentada.

## 10. Flujos alternativos que afectan la interpretación

### 10.1 Recálculo

`RecalcularPuntajesHandler`:

- Obtiene procesos cuyo estado persistido es `Activo`.
- Además exige fecha de cierre futura y embedding de proceso.
- Evalúa uno o todos los proveedores con embedding.
- Usa el umbral global inclusivo `0.40`.
- Persiste cada par que supera el umbral.
- No elimina pares que ya no superan el umbral.
- No genera ni envía alertas.

### 10.2 Búsqueda compatible

`BuscarProcesosHandler` usa el umbral global inclusivo `0.40`, aplica el límite solicitado **por proveedor**, exige fecha de cierre futura, recalcula el puntaje en memoria y omite etiqueta `Descartar`. No persiste el puntaje ni exige estado `Activo`.

Si se consultan varios proveedores, el resultado agregado puede superar el límite solicitado porque el límite se aplica en cada búsqueda individual.

### 10.3 Detalle de proceso

`ObtenerDetalleHandler` selecciona el proveedor con mayor similitud y exige que esa similitud sea `>= 0.40`. Calcula un puntaje en memoria, pero no exige que el proceso esté vigente y no descarta el DTO si la etiqueta calculada es `Descartar`.

### 10.4 Cálculo directo

`CalcularPuntajeHandler` persiste un puntaje para un proceso y proveedor existentes con embeddings cuando alcanza el umbral global inclusivo `0.40`; no exige vigencia, estado, UNSPSC ni palabra clave. No se encontró una ruta HTTP que invoque este comando, pero el handler forma parte del código de aplicación.

## 11. Ejemplos AS-IS

### Ejemplo A: proceso que entra a evaluación

Supuestos:

- DTO válido, abierto, publicado, en fase de ofertas y clasificado como `Ley80`.
- Coincide por UNSPSC con un proveedor que tiene embedding.
- El ID no existe en la base.
- Similitud calculada: `0.44`.

Resultado:

- El proceso se mapea, embebe y persiste.
- Como `0.44 >= 0.40`, se ejecuta el scoring.
- Se crea un puntaje para el par proceso-proveedor.
- La posibilidad de alerta depende del resultado de la evaluación y de la configuración del proveedor.
- El contador `ProcesosNuevos` aumenta en uno.

### Ejemplo B: `Analizar` y log simulado

Supuestos:

- Similitud: `0.50`.
- Capacidad suficiente: sí.
- Sin contadores de competencia: valor neutral `6`.
- Proveedor con chat de Telegram.

Cálculo:

```text
Similitud  = 0.50 * 35 = 17.5
Requisitos = 15 + 10   = 25
Tiempo     = 0
Competencia = 6
Entidad    = 0
Total      = 48.5
Etiqueta   = Analizar
```

Resultado:

- Se persiste el puntaje del par.
- Cumple el umbral de alerta de sincronización (`>= 40`).
- Solo se escribe el log de alerta simulada; no se llama a Telegram en ese momento.
- Una ejecución posterior de `EnviarAlertasHandler` sí puede intentar enviarlo si el proceso sigue vigente.

### Ejemplo C: `Proponer` cerca del máximo actual

Supuestos:

- Similitud: `0.95`.
- Capacidad suficiente: sí.
- Cero competidores: `12` puntos.

```text
Total = (0.95 * 35) + 25 + 12 = 70.25
Etiqueta = Proponer
```

Con los componentes desactivados, una similitud alta y los máximos de requisitos y competencia son necesarios para superar 70.

### Ejemplo D: proceso exclusivo para ESAL

Supuestos:

- `TipoContrato` contiene `092` y `2017`.
- Similitud: `0.95`.

Resultado durante la evaluación inicial del objeto recién construido:

- Puntaje total `0`.
- Etiqueta `Descartar`.
- `EsInhabilitado = true`.
- No cumple el umbral de alerta.

### Ejemplo E: proceso ya almacenado

Aunque SECOP vuelva a devolverlo con una fecha, objeto o estado distinto, `SincronizarProcesosHandler` detecta el mismo ID y omite todo el procesamiento. Los datos y puntajes existentes no se actualizan por esa sincronización.

## 12. Inconsistencias y gotchas confirmados

| Hallazgo | Comportamiento AS-IS |
|---|---|
| Configuración no gobierna umbrales | Los valores `Scoring:UmbralSimilitudMinima`, `UmbralAnalizar` y `UmbralProponer` no se inyectan en estos handlers; se usan constantes codificadas |
| Nombre engañoso | `UmbralAlertaProponer = 40` incluye `Analizar`, no solo `Proponer` |
| Sincronización sin envío real | La llamada a Telegram está comentada y reemplazada por un log simulado |
| Envío diferido repetible | El worker diario sí intenta enviar procesos y no registra ni deduplica mensajes |
| RUP no inhabilita | La validación está comentada y se otorgan los 15 puntos base aunque esté vencido |
| Componentes inactivos | Tiempo e historial de entidad siempre valen `0` |
| Máximo práctico reducido | Con similitud hasta `1`, el máximo actual es `72/100` |
| Proceso no equivale a puntaje | El proceso se guarda antes del umbral semántico |
| Puntaje no equivale a proceso único | Cada registro representa un par proceso-proveedor |
| Puntaje no equivale a mensaje | Puede existir sin chat, con envío simulado o sin entrega confirmada |
| Existentes no se refrescan | Un ID ya persistido omite actualización, scoring y alerta en sincronización |
| Puntajes bajo el nuevo umbral permanecen | El recálculo no borra registros previos cuando la nueva similitud es `< 0.40` |
| Upsert no conserva versiones del mismo par | Si un par sí se recalcula, elimina el registro anterior y crea otro |
| Advertencias de sincronización pueden perderse | El recálculo reemplaza el puntaje sin volver a agregar procedencia por texto ni posible régimen especial |
| Palabras clave solo miran objeto | Una coincidencia presente únicamente en el título no crea candidato por texto |
| Categorías adicionales no seleccionan candidatos | Se persisten, pero no se consultan ni comparan para la asociación UNSPSC |
| Fuente UNSPSC sin códigos | Si no hay códigos globales, esa llamada consulta todo por fecha pero sus resultados siguen etiquetados internamente como fuente UNSPSC |
| Fecha inválida puede persistirse | En fase conocida, una fecha ausente/inválida pasa aplicabilidad y luego se mapea como `DateTime.MinValue` |
| Presupuesto inválido favorece requisitos | Se mapea a `0`, haciendo trivial la cobertura financiera del 15% |
| Bandera `keyword-only` es por proceso | Puede atribuir incorrectamente la procedencia de un puntaje particular |
| Publicado puede terminar adjudicado | Los campos de adjudicación se evalúan después del filtro y tienen precedencia en el estado persistido |
| Comentarios del scoring están desactualizados | Describen cinco componentes activos y RUP inhabilitante, pero el cuerpo ejecutable no lo hace |

## 13. Fuentes de código

### Obtención y filtros

- `src/Secop.Infrastructure/ExternalServices/SecopApiClient.cs`: consultas SODA, ventana temporal, lotes UNSPSC, límites y manejo de errores.
- `src/Secop.Application/DTOs/SecopProcesoDto.cs`: mapeo del dataset, `EsValido`, `EstaAbiertoParaAplicar`, modalidad, estado y clasificación.
- `src/Secop.Application/UseCases/Procesos/SincronizarProcesos/SincronizarProcesosHandler.cs`: pipeline principal, candidatos, umbrales, persistencia y simulación de alerta.
- `src/Secop.Domain/ValueObjects/CodigoUnspsc.cs`: validación y descomposición de códigos.

### Representación semántica y búsqueda

- `src/Secop.Application/Services/ConstructorTextoSemantico.cs`: texto de proveedor y proceso.
- `src/Secop.Infrastructure/ExternalServices/OpenAiEmbeddingService.cs`: generación de embeddings y similitud coseno.
- `src/Secop.Application/UseCases/Procesos/BuscarProcesosCompatibles/BuscarProcesosHandler.cs`: búsqueda compatible en memoria de aplicación.
- `src/Secop.Infrastructure/Persistence/Repositories/ProcesoRepository.cs`: consulta vectorial pgvector y filtros persistidos.
- `src/Secop.Application/UseCases/Procesos/ObtenerDetalleProceso/ObtenerDetalleHandler.cs`: selección del mejor proveedor para detalle.

### Dominio y scoring

- `src/Secop.Domain/Entities/Proveedor.cs`: vigencia RUP y cobertura financiera.
- `src/Secop.Domain/Entities/Proceso.cs`: vigencia, días hábiles y detección ESAL.
- `src/Secop.Domain/Entities/Puntaje.cs`: componentes, etiqueta, advertencias e inhabilitación.
- `src/Secop.Infrastructure/Scoring/ScoringService.cs`: fórmula ejecutable, clasificación y competencia.
- `src/Secop.Domain/Services/DetectorRegimenEspecial.cs` y `src/Secop.Domain/Constants/PatronesRegimenEspecial.cs`: advertencia por nombre de entidad.
- `src/Secop.Application/UseCases/Puntajes/RecalcularPuntajes/RecalcularPuntajesHandler.cs`: recálculo con umbral global inclusivo `0.40`.
- `src/Secop.Application/UseCases/Puntajes/CalcularPuntaje/CalcularPuntajeHandler.cs`: cálculo directo con el mismo umbral global.

### Persistencia y alertas

- `src/Secop.Infrastructure/Persistence/Repositories/PuntajeRepository.cs`: upsert por proceso-proveedor.
- `src/Secop.Infrastructure/Persistence/Configurations/ProcesoConfiguration.cs`: columnas del proceso y embedding.
- `src/Secop.Infrastructure/Persistence/Configurations/PuntajeConfiguration.cs`: restricción única del par.
- `src/Secop.Application/UseCases/Alertas/EnviarAlertasProveedor/EnviarAlertasHandler.cs`: selección e intento real de alertas.
- `src/Secop.Infrastructure/ExternalServices/Telegram/TelegramAlertaService.cs`: envío y manejo de fallos de Telegram.
- `src/Secop.Infrastructure/ExternalServices/Telegram/ProcesoMessageFormatter.cs`: etiqueta y contenido del mensaje.
- `src/Secop.Worker/Workers/RupAlertaWorker.cs`: ejecución diaria del handler de alertas.

## 14. Regla de lectura operativa

Para responder cuántos procesos fueron realmente relevantes o alertados, no debe usarse una sola cifra:

1. `ProcesosNuevos` indica filas nuevas en `procesos`.
2. El número de filas nuevas o reemplazadas en `puntajes` indica pares proceso-proveedor que llegaron al scoring en ese flujo.
3. Los puntajes `>= 40` con chat configurado indican alertas **potenciales** durante sincronización, pero allí solo generan logs simulados.
4. Los puntajes no descartados, no inhabilitados y con proceso vigente son candidatos al envío diferido.
5. Sin persistencia del ID devuelto por Telegram, el sistema no permite demostrar desde su propia base cuántos mensajes de proceso se entregaron realmente.
