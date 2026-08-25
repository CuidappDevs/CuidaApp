namespace CUIDAPP_ADMINISTRATIVO.Services;

// Datos de ejemplo portados del diseño de Claude Design ("Panel Admin CUIDAPP").
// Sustituir por llamadas a la API (Admin/cuidadores-pendientes, etc.) cuando exista el backend.

public record StatusPill(string Bg, string Fg);

public record Partner(
    string Name, string Category, string Cedula, string Tel,
    string Status, string Key, string Date, string Sector);

public record Client(
    string Name, string Mail, string Cedula, string Tel,
    string Status, string Key);

public record QueueItem(string Initials, string Name, string Meta, string Docs, bool DocsAlert, string When, int PartnerIndex);

public record WeekBar(string Label, int H1, int H2);

public record ActivityItem(string Dot, string Text, string Who);

public record StatCard(string Label, string Value, string Note, string Color);

public record ServiceRow(string Type, string Partner, string Date, string State, string Bg, string Fg);

public record FlagRow(string Label, string Value, string Fg);

public record TimelineItem(string Dot, string Text, string When);

public record DocCard(string Title, string Meta, string File);

public record KeyVal(string Key, string Value);

public static class AdminMockData
{
    // Mapa de estados -> colores de pastilla (fondo, texto)
    public static StatusPill Pill(string key) => key switch
    {
        "verificada" => new("#ECFDF5", "#059669"),
        "pendiente"  => new("#FEF3C7", "#B45309"),
        "incompleta" => new("#FEE2E2", "#DC2626"),
        "activa"     => new("#ECFDF5", "#059669"),
        "inactiva"   => new("#F3F4F6", "#6B7280"),
        "suspendida" => new("#FEE2E2", "#DC2626"),
        _            => new("#F3F4F6", "#6B7280"),
    };

    public static string Initials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Concat(parts.Take(2).Select(p => p[0]));
    }

    public static readonly IReadOnlyList<Partner> Partners = new List<Partner>
    {
        new("Yudelka Peña Rosario", "Cuidado de adultos mayores", "402-1938472-6", "(809) 745-2210", "Pendiente", "pendiente", "18 de agosto, 2026", "Los Prados, D.N."),
        new("Marisol Jiménez Ureña", "Limpieza del hogar", "001-1772903-1", "(829) 331-8890", "Pendiente", "pendiente", "18 de agosto, 2026", "Villa Mella"),
        new("Ana Luisa Cabrera", "Niñera", "223-0091823-4", "(809) 512-4477", "Documentos incompletos", "incompleta", "17 de agosto, 2026", "Naco, D.N."),
        new("Rosa Delgado Matos", "Cocina", "402-2210934-8", "(849) 220-1132", "Verificada", "verificada", "12 de agosto, 2026", "Santiago"),
        new("Carmen Altagracia Núñez", "Limpieza del hogar", "031-0448271-2", "(809) 664-9021", "Verificada", "verificada", "10 de agosto, 2026", "San Cristóbal"),
        new("Elizabeth Santana Paulino", "Cuidado de adultos mayores", "402-3390182-7", "(829) 887-3345", "Pendiente", "pendiente", "16 de agosto, 2026", "Herrera"),
        new("Nikaury Ferreras Díaz", "Niñera", "001-2093847-5", "(809) 209-7764", "Verificada", "verificada", "5 de agosto, 2026", "Bella Vista, D.N."),
    };

    public static readonly IReadOnlyList<Client> Clients = new List<Client>
    {
        new("Familia Rodríguez Bello", "jrodriguez@gmail.com", "001-1029384-7", "(809) 555-0192", "Activa", "activa"),
        new("Patricia Guerrero Lama", "pguerrero@outlook.com", "402-1192837-3", "(829) 441-2093", "Activa", "activa"),
        new("Luis Manuel Bonilla", "lmbonilla@gmail.com", "031-0928374-1", "(809) 776-3312", "Inactiva", "inactiva"),
        new("Carolina Espaillat", "cespaillat@icloud.com", "001-3320918-9", "(849) 302-1188", "Activa", "activa"),
        new("Familia Then Marte", "athen@gmail.com", "223-1029384-6", "(809) 620-4471", "Suspendida", "suspendida"),
        new("Ramón Antonio Vásquez", "ravasquez@gmail.com", "402-0091827-4", "(829) 118-9930", "Activa", "activa"),
        new("Mercedes Lantigua", "mlantigua@gmail.com", "001-4483920-2", "(809) 998-2201", "Inactiva", "inactiva"),
    };

    // ----- Dashboard -----
    public static readonly IReadOnlyList<StatCard> Stats = new List<StatCard>
    {
        new("Pendientes de revisión", "12", "3 esperan más de 48 horas", "#B45309"),
        new("Aprobadas este mes", "34", "de 41 solicitudes recibidas", "#059669"),
        new("Rechazadas este mes", "7", "Documentos ilegibles o vencidos", "#111827"),
    };

    public static IReadOnlyList<QueueItem> Queue()
    {
        var whens = new[] { "Hace 2 horas", "Hace 5 horas", "Ayer", "Hace 3 días" };
        return Partners.Take(4).Select((p, i) => new QueueItem(
            Initials(p.Name), p.Name, $"{p.Category} · {p.Sector}",
            p.Key == "incompleta" ? "1 de 2 documentos" : "2 de 2 documentos",
            p.Key == "incompleta", whens[i], i)).ToList();
    }

    public static readonly IReadOnlyList<WeekBar> Weeks = new List<WeekBar>
    {
        new("21 jul", 38, 56), new("28 jul", 52, 44), new("4 ago", 46, 70),
        new("11 ago", 68, 58), new("18 ago", 84, 72), new("25 ago", 34, 30),
    };

    public static readonly IReadOnlyList<ActivityItem> Activity = new List<ActivityItem>
    {
        new("#10B981", "Rosa Delgado Matos fue aprobada como Care Partner de Cocina.", "Laura Méndez · hace 40 min"),
        new("#EF4444", "Solicitud de Juana Ramírez rechazada: carta de antecedentes vencida.", "Pedro Alcántara · hace 3 horas"),
        new("#0253A5", "Se solicitó nueva foto de cédula a Ana Luisa Cabrera.", "Laura Méndez · ayer"),
        new("#10B981", "Carmen Altagracia Núñez fue aprobada como Care Partner de Limpieza.", "Pedro Alcántara · ayer"),
    };

    // ----- Ficha de Care Partner -----
    public static IReadOnlyList<KeyVal> PartnerFields(Partner p) => new List<KeyVal>
    {
        new("Cédula", p.Cedula), new("Teléfono", p.Tel),
        new("Correo", p.Name.Split(' ')[0].ToLowerInvariant() + "@gmail.com"),
        new("Sector de cobertura", p.Sector), new("Experiencia declarada", "6 años"),
        new("Disponibilidad", "Lunes a sábado"),
    };

    public static IReadOnlyList<DocCard> PartnerDocs(Partner p) => new List<DocCard>
    {
        new("Cédula de identidad", "Subida el " + p.Date, "cedula-frente.jpg"),
        new("Carta de antecedentes penales", "Emitida el 2 de agosto, 2026", "antecedentes.pdf"),
    };

    public static IReadOnlyList<TimelineItem> PartnerHistory(Partner p) => new List<TimelineItem>
    {
        new("#0253A5", "Solicitud recibida desde la app de Care Partners.", p.Date + " · 9:14 a. m."),
        new("#0253A5", "Documentos cargados: cédula y antecedentes penales.", p.Date + " · 9:31 a. m."),
        new("#B45309", "En espera de revisión administrativa.", "Actualmente"),
    };

    public static readonly IReadOnlyList<(string Label, bool On)> PartnerChecks = new List<(string, bool)>
    {
        ("La foto de la cédula es legible y vigente", true),
        ("Los antecedentes penales no registran hallazgos", true),
        ("El nombre coincide en ambos documentos", false),
    };

    // ----- Ficha de Cliente -----
    public static IReadOnlyList<KeyVal> ClientFields(Client c) => new List<KeyVal>
    {
        new("Cédula", c.Cedula), new("Teléfono", c.Tel), new("Correo", c.Mail),
        new("Dirección", "Calle Pedro Livio Cedeño 24"),
        new("Método de pago", "Tarjeta •••• 4417 verificada"),
        new("Servicios contratados", "18 en total"),
    };

    private static ServiceRow Sv(string type, string partner, string date, string state)
    {
        var (bg, fg) = state switch
        {
            "Completado" => ("#ECFDF5", "#059669"),
            "En curso"   => ("#E3EEFA", "#0253A5"),
            _            => ("#F3F4F6", "#6B7280"),
        };
        return new ServiceRow(type, partner, date, state, bg, fg);
    }

    public static readonly IReadOnlyList<ServiceRow> ClientServices = new List<ServiceRow>
    {
        Sv("Limpieza del hogar", "Carmen Altagracia Núñez", "22 ago 2026", "En curso"),
        Sv("Niñera", "Nikaury Ferreras Díaz", "15 ago 2026", "Completado"),
        Sv("Cocina", "Rosa Delgado Matos", "9 ago 2026", "Completado"),
        Sv("Limpieza del hogar", "Carmen Altagracia Núñez", "1 ago 2026", "Cancelado"),
    };

    public static readonly IReadOnlyList<FlagRow> ClientFlags = new List<FlagRow>
    {
        new("Identidad del titular", "Verificada", "#059669"),
        new("Método de pago", "Verificado", "#059669"),
        new("Reportes de Care Partners", "Ninguno", "#6B7280"),
    };

    public static readonly IReadOnlyList<TimelineItem> ClientHistory = new List<TimelineItem>
    {
        new("#0253A5", "Cuenta creada desde la app de clientes.", "12 de marzo, 2025"),
        new("#10B981", "Método de pago verificado con cargo de prueba.", "12 de marzo, 2025"),
        new("#0253A5", "Último servicio solicitado: limpieza del hogar.", "22 de agosto, 2026"),
    };
}
