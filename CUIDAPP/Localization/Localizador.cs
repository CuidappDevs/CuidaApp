using CUIDAPP.Localization;
using System.ComponentModel;
using System.Globalization;
using System.Text.Json;

namespace CUIDAPP.Localization
{
    /// <summary>
    /// Textos de la app por idioma. Los diccionarios viven en Resources/Strings/{codigo}.json
    /// (recursos incrustados, clave → texto). El español es el idioma base: si falta una clave
    /// en el idioma activo se usa la del español, y si tampoco existe, se muestra la clave.
    /// </summary>
    public class Localizador : INotifyPropertyChanged
    {
        public const string IdiomaBase = "es";
        private const string PreferenciaIdioma = "Idioma";

        // Para agregar un idioma: crear Resources/Strings/{codigo}.json y añadirlo aquí.
        public static readonly IReadOnlyList<(string Codigo, string Nombre)> Idiomas = new[]
        {
            ("es", "Español"),
            ("en", "English"),
            ("ht", "Kreyòl"),
        };


        private readonly Dictionary<string, Dictionary<string, string>> _diccionarios = new();
        private Dictionary<string, string> _actual;
        private readonly Dictionary<string, string> _baseEs;

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>Se dispara tras cambiar de idioma. Las pantallas que arman textos por código
        /// (no por {loc:T}) se suscriben mientras están visibles para volver a pintarlos.</summary>
        public event Action? IdiomaCambiado;

        public string Codigo { get; private set; } = IdiomaBase;
        public static CultureInfo Cultura { get; private set; } = new CultureInfo(IdiomaBase);

        // Debe declararse DESPUÉS de Cultura: el constructor la asigna y el inicializador estático
        // de Cultura la pisaría si se ejecutara después.
        public static Localizador Instancia { get; } = new();

        private Localizador()
        {
            _baseEs = Cargar(IdiomaBase);
            _actual = _baseEs;
            Aplicar(IdiomaGuardadoODelDispositivo(), notificar: false);
        }

        public string this[string clave] => Obtener(clave);

        // Un objeto observable por clave: el XAML se enlaza a su propiedad Texto (binding simple,
        // más fiable que un binding de indexador) y se refresca al cambiar el idioma.
        private readonly Dictionary<string, TextoClave> _textos = new();

        public TextoClave Para(string clave)
        {
            if (!_textos.TryGetValue(clave, out var texto))
                _textos[clave] = texto = new TextoClave(clave);
            return texto;
        }

        public static string T(string clave) => Instancia.Obtener(clave);

        /// <summary>Texto de catálogo que viene de la base de datos (especialidades, motivos...).
        /// Si hay traducción en dato_{texto} se usa; si no, se muestra tal cual.</summary>
        public static string D(string? texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return texto ?? "";
            var clave = "dato_" + System.Text.RegularExpressions.Regex.Replace(
                new string(texto.Normalize(System.Text.NormalizationForm.FormD)
                    .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray())
                    .ToLowerInvariant(), "[^a-z0-9]+", "_").Trim('_');
            return Instancia._actual.TryGetValue(clave, out var traducido) ? traducido : texto;
        }

        public static string FechaLarga(DateTime fecha) => fecha.ToString(T("fmt_fecha_larga"), Cultura);

        public static string DiaMes(DateTime fecha) => fecha.ToString(T("fmt_dia_mes"), Cultura);

        public static string F(string clave, params object?[] args)
        {
            var plantilla = Instancia.Obtener(clave);
            try { return string.Format(Cultura, plantilla, args); }
            catch (FormatException) { return plantilla; }
        }

        private string Obtener(string clave)
        {
            if (_actual.TryGetValue(clave, out var texto)) return texto;
            if (_baseEs.TryGetValue(clave, out texto)) return texto;
            return clave;
        }

        /// <summary>Cambia el idioma, lo guarda y refresca todos los {loc:T ...} ya mostrados.</summary>
        public void Cambiar(string codigo)
        {
            if (codigo == Codigo || !Idiomas.Any(i => i.Codigo == codigo))
                return;

            Preferences.Default.Set(PreferenciaIdioma, codigo);
            Aplicar(codigo, notificar: true);
        }

        private void Aplicar(string codigo, bool notificar)
        {
            Codigo = codigo;
            _actual = Cargar(codigo);

            Cultura = CrearCultura(codigo);
            CultureInfo.DefaultThreadCurrentCulture = Cultura;
            CultureInfo.DefaultThreadCurrentUICulture = Cultura;
            CultureInfo.CurrentCulture = Cultura;
            CultureInfo.CurrentUICulture = Cultura;

            if (notificar)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
                foreach (var texto in _textos.Values.ToList())
                    texto.Refrescar();
                IdiomaCambiado?.Invoke();
            }
        }

        // Fechas y números. Si el sistema no trae formatos para un idioma (p. ej. creol "ht" en algunos
        // Android), se usan los del país más cercano en vez de fallar.
        private static CultureInfo CrearCultura(string codigo)
        {
            foreach (var nombre in codigo == "ht" ? new[] { "ht-HT", "fr-HT", "fr" } : new[] { codigo })
            {
                try
                {
                    var cultura = new CultureInfo(nombre);
                    if (!string.IsNullOrEmpty(cultura.DateTimeFormat.MonthNames[0]))
                        return cultura;
                }
                catch (CultureNotFoundException) { }
            }
            return new CultureInfo(IdiomaBase);
        }

        private static string IdiomaGuardadoODelDispositivo()
        {
            var guardado = Preferences.Default.Get(PreferenciaIdioma, "");
            if (Idiomas.Any(i => i.Codigo == guardado))
                return guardado;

            var dispositivo = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            return Idiomas.Any(i => i.Codigo == dispositivo) ? dispositivo : IdiomaBase;
        }

        private Dictionary<string, string> Cargar(string codigo)
        {
            if (_diccionarios.TryGetValue(codigo, out var existente))
                return existente;

            var asm = typeof(Localizador).Assembly;
            var recurso = asm.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith($".Strings.{codigo}.json", StringComparison.OrdinalIgnoreCase));

            var dic = new Dictionary<string, string>();
            if (recurso != null)
            {
                using var stream = asm.GetManifestResourceStream(recurso)!;
                using var doc = JsonDocument.Parse(stream);
                foreach (var entrada in doc.RootElement.EnumerateObject())
                    dic[entrada.Name] = entrada.Value.GetString() ?? "";
            }

            _diccionarios[codigo] = dic;
            return dic;
        }
    }

    /// <summary>Texto traducido observable para una clave (lo usa {loc:T clave}).</summary>
    public sealed class TextoClave : INotifyPropertyChanged
    {
        private readonly string _clave;

        public TextoClave(string clave) => _clave = clave;

        public string Texto => Localizador.T(_clave);

        public event PropertyChangedEventHandler? PropertyChanged;

        internal void Refrescar() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Texto)));
    }
}
