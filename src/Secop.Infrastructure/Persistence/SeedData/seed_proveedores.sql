-- ============================================================
-- SEED: Proveedores del grupo empresarial
-- Base de datos: Supabase (PostgreSQL + pgvector)
-- Tabla creada por EF Core migrations — no recrear aquí.
-- 🔴 = reemplaza con datos reales antes de ejecutar
-- ============================================================

-- Empresa 1 — Logística y Eventos
INSERT INTO proveedores (id, nombre, nit, rup_vigencia, capacidad_financiera, codigos_unspsc, experiencia, telegram_chat_id, creado_en, actualizado_en)
VALUES (
    gen_random_uuid(),
    'Logística y Eventos SAS',                          -- 🔴 razón social real
    '900100001-1',                                       -- 🔴 NIT real
    '2026-03-31 00:00:00+00',                            -- 🔴 fecha vigencia RUP
    7000000000.00,
    ARRAY[
        '80101500',
        '78121801',
        '78111808',
        '25101700',
        '80111500',
        '80141600',
        '93141700'
    ],
    ARRAY[
        'Organización de eventos deportivos institucionales para entidades del Estado',
        'Planes de bienestar laboral para funcionarios públicos',
        'Suministro y arrendamiento de vehículos para entidades oficiales',
        'Logística integral para eventos gubernamentales de gran escala'
    ],
    NULL,                                                -- 🔴 telegram_chat_id real
    NOW(),
    NOW()
);

-- Empresa 2 — Publicidad y Tecnología
INSERT INTO proveedores (id, nombre, nit, rup_vigencia, capacidad_financiera, codigos_unspsc, experiencia, telegram_chat_id, creado_en, actualizado_en)
VALUES (
    gen_random_uuid(),
    'Publicidad y Tecnología SAS',                      -- 🔴 razón social real
    '900100002-2',                                       -- 🔴 NIT real
    '2026-03-31 00:00:00+00',                            -- 🔴 fecha vigencia RUP
    4000000000.00,
    ARRAY[
        '82101700',
        '82101500',
        '82101600',
        '82111500',
        '43230000',
        '43211500',
        '43191500',
        '43233200',
        '81161700'
    ],
    ARRAY[
        'Suministro de equipos de cómputo y hardware para entidades públicas',
        'Campañas de publicidad institucional BTL y ATL para entidades del Estado',
        'Desarrollo e implementación de plataformas tecnológicas para el sector público',
        'Servicios de marketing digital y gestión de redes sociales institucionales',
        'Importación y distribución de equipos tecnológicos especializados'
    ],
    NULL,                                                -- 🔴 telegram_chat_id real
    NOW(),
    NOW()
);

-- Empresa 3 — Asesorías Jurídicas y Tributarias
INSERT INTO proveedores (id, nombre, nit, rup_vigencia, capacidad_financiera, codigos_unspsc, experiencia, telegram_chat_id, creado_en, actualizado_en)
VALUES (
    gen_random_uuid(),
    'Asesorías Jurídicas y Tributarias SAS',            -- 🔴 razón social real
    '900100003-3',                                       -- 🔴 NIT real
    '2026-03-31 00:00:00+00',                            -- 🔴 fecha vigencia RUP
    800000000.00,
    ARRAY[
        '80111600',
        '80111700',
        '80101600',
        '80101500',
        '80121700'
    ],
    ARRAY[
        'Consultoría jurídica en contratación estatal para alcaldías y gobernaciones',
        'Asesoría tributaria institucional para empresas industriales del Estado',
        'Interventoría jurídica a contratos de obra pública',
        'Estructuración y revisión de pliegos de condiciones para entidades públicas',
        'Auditoría jurídica de contratos en ejecución'
    ],
    NULL,                                                -- 🔴 telegram_chat_id real
    NOW(),
    NOW()
);

-- Empresa 4 — ESAL
INSERT INTO proveedores (id, nombre, nit, rup_vigencia, capacidad_financiera, codigos_unspsc, experiencia, telegram_chat_id, creado_en, actualizado_en)
VALUES (
    gen_random_uuid(),
    'Fundación Ambiente y Bienestar',                   -- 🔴 razón social real
    '900100004-4',                                       -- 🔴 NIT real
    '2026-03-31 00:00:00+00',                            -- 🔴 fecha referencial (ESAL puede no requerir RUP)
    14000000000.00,
    ARRAY[
        '77101500',
        '78101800',
        '10101500'
    ],
    ARRAY[
        'Proyectos de educación ambiental y reforestación para alcaldías',
        'Campañas de esterilización y bienestar animal para secretarías de medio ambiente',
        'Organización de turismo pedagógico y rutas ecológicas para programas sociales',
        'Gestión de residuos y campañas de sostenibilidad institucional'
    ],
    NULL,                                                -- 🔴 telegram_chat_id real
    NOW(),
    NOW()
);

-- Verificar inserción
SELECT nombre, nit, rup_vigencia,
       TO_CHAR(capacidad_financiera, 'FM$999,999,999,999') AS capacidad,
       array_length(codigos_unspsc, 1) AS total_unspsc
FROM proveedores
ORDER BY capacidad_financiera DESC;
