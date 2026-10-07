-- Catálogo de nacionalidades (para el perfil del cliente y del cuidador).
-- Por ahora solo crea la tabla y la llena; ningún perfil, SP ni pantalla la usa todavía.
--
-- Nombre: gentilicio como se mostrará (femenino/neutro según uso en la app: "Dominicana").
-- Pais:   nombre del país.  CodigoIso: ISO 3166-1 alfa-2 (único).
-- Activo = 0: deja de ofrecerse sin borrar la fila (los perfiles que ya la tengan no se rompen).
--
-- Idempotente: crea la tabla si no existe e inserta solo los códigos que falten.
-- Ejecutar con sqlcmd -f 65001 (UTF-8) para no dañar acentos ni la ñ.

IF OBJECT_ID(N'dbo.Nacionalidades', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Nacionalidades (
        Id        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Nacionalidades PRIMARY KEY,
        Nombre    NVARCHAR(80)  NOT NULL,
        Pais      NVARCHAR(80)  NOT NULL,
        CodigoIso CHAR(2)       NOT NULL CONSTRAINT UQ_Nacionalidades_CodigoIso UNIQUE,
        Activo    BIT           NOT NULL CONSTRAINT DF_Nacionalidades_Activo DEFAULT (1)
    );
END;
GO

INSERT INTO dbo.Nacionalidades (Nombre, Pais, CodigoIso)
SELECT v.Nombre, v.Pais, v.CodigoIso
FROM (VALUES
    (N'Dominicana',      N'República Dominicana', 'DO'),
    (N'Haitiana',        N'Haití',                'HT'),
    (N'Venezolana',      N'Venezuela',            'VE'),
    (N'Colombiana',      N'Colombia',             'CO'),
    (N'Cubana',          N'Cuba',                 'CU'),
    (N'Puertorriqueña',  N'Puerto Rico',          'PR'),
    (N'Estadounidense',  N'Estados Unidos',       'US'),
    (N'Canadiense',      N'Canadá',               'CA'),
    (N'Mexicana',        N'México',               'MX'),
    (N'Guatemalteca',    N'Guatemala',            'GT'),
    (N'Hondureña',       N'Honduras',             'HN'),
    (N'Salvadoreña',     N'El Salvador',          'SV'),
    (N'Nicaragüense',    N'Nicaragua',            'NI'),
    (N'Costarricense',   N'Costa Rica',           'CR'),
    (N'Panameña',        N'Panamá',               'PA'),
    (N'Jamaicana',       N'Jamaica',              'JM'),
    (N'Trinitense',      N'Trinidad y Tobago',    'TT'),
    (N'Ecuatoriana',     N'Ecuador',              'EC'),
    (N'Peruana',         N'Perú',                 'PE'),
    (N'Boliviana',       N'Bolivia',              'BO'),
    (N'Chilena',         N'Chile',                'CL'),
    (N'Argentina',       N'Argentina',            'AR'),
    (N'Uruguaya',        N'Uruguay',              'UY'),
    (N'Paraguaya',       N'Paraguay',             'PY'),
    (N'Brasileña',       N'Brasil',               'BR'),
    (N'Española',        N'España',               'ES'),
    (N'Portuguesa',      N'Portugal',             'PT'),
    (N'Francesa',        N'Francia',              'FR'),
    (N'Italiana',        N'Italia',               'IT'),
    (N'Alemana',         N'Alemania',             'DE'),
    (N'Británica',       N'Reino Unido',          'GB'),
    (N'Neerlandesa',     N'Países Bajos',         'NL'),
    (N'Suiza',           N'Suiza',                'CH'),
    (N'Rusa',            N'Rusia',                'RU'),
    (N'Ucraniana',       N'Ucrania',              'UA'),
    (N'China',           N'China',                'CN'),
    (N'Coreana',         N'Corea del Sur',        'KR'),
    (N'Japonesa',        N'Japón',                'JP'),
    (N'India',           N'India',                'IN'),
    (N'Filipina',        N'Filipinas',            'PH'),
    (N'Libanesa',        N'Líbano',               'LB'),
    (N'Israelí',         N'Israel',               'IL'),
    (N'Nigeriana',       N'Nigeria',              'NG'),
    (N'Sudafricana',     N'Sudáfrica',            'ZA'),
    (N'Australiana',     N'Australia',            'AU')
) AS v (Nombre, Pais, CodigoIso)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Nacionalidades n WHERE n.CodigoIso = v.CodigoIso);
GO

SELECT COUNT(*) AS Total FROM dbo.Nacionalidades;
