using System.Text;

namespace CUIDAPP_API.Services.Email
{
    /// <summary>
    /// Correo de bienvenida al registrarse, con el mismo diseño de los recibos.
    /// - Cliente: cuenta lista, cómo funciona y por qué es seguro.
    /// - Cuidador: registro recibido, documentos en revisión (línea de progreso) y cómo seguirla en la app.
    ///   Mientras no lo aprueban solo ve la pantalla de revisión: no sugerir acciones de otras pantallas.
    /// </summary>
    public class BienvenidaService : PlantillaCorreo
    {
        public BienvenidaService(IConfiguration config) : base(config) { }

        public Task EnviarClienteAsync(string email, string nombre)
            => string.IsNullOrWhiteSpace(email) ? Task.CompletedTask
             : EnviarSeguroAsync(email, $"¡Bienvenido a CuidApp, {PrimerNombre(nombre)}!", HtmlCliente(nombre));

        public Task EnviarCuidadorAsync(string email, string nombre)
            => string.IsNullOrWhiteSpace(email) ? Task.CompletedTask
             : EnviarSeguroAsync(email, "Recibimos tu registro en CuidApp · Documentos en revisión", HtmlCuidador(nombre));

        public string HtmlCliente(string nombre)
        {
            var n = E(PrimerNombre(nombre));
            var sb = new StringBuilder();
            sb.Append(Encabezado(DateTime.Now, $"¡Bienvenido a CuidApp, {n}!", "Tu cuenta está lista. Ya puedes encontrar a la persona ideal para ayudarte en casa.", "hero_hogar"));

            sb.Append(Seccion($"""
                {Titulo("Así de fácil funciona")}
                {Punto("persona", "1. Elige el servicio", "Limpieza, cuidado de niños, de adultos mayores y más.")}
                {Punto("escudo", "2. Escoge a tu cuidador", "Mira su perfil, su tarifa por hora y sus calificaciones.")}
                {Punto("calendario", "3. Solicita y síguelo en vivo", "Recibe avisos cuando acepte, llegue y termine. Puedes chatear con él o ella.")}
                {Punto("estrella", "4. Confirma y califica", "Al terminar confirmas el servicio, dejas una propina si quieres y lo calificas.")}
                """));

            sb.Append(Seccion($"""
                <tr><td colspan="2">{Destacado("escudo", "Tu seguridad es primero", "Todos los cuidadores pasan una verificación de identidad y antecedentes antes de poder trabajar. Cada servicio empieza y termina con un código PIN que solo tú conoces.")}</td></tr>
                """));

            sb.Append(Seccion($"""
                {Tarjetas(
                    ("ubicacion", "Guarda tus direcciones", "Agrega tu casa en Mis ubicaciones para reservar más rápido."),
                    ("soporte", "¿Tienes dudas?", "Escríbenos desde Soporte en la app y te ayudamos."))}
                """, fondo: Fondo));

            sb.Append(Pie("Recibiste este correo porque creaste una cuenta en CuidApp."));
            return Envolver("Bienvenido a CuidApp", sb.ToString());
        }

        public string HtmlCuidador(string nombre)
        {
            var n = E(PrimerNombre(nombre));
            var sb = new StringBuilder();
            sb.Append(Encabezado(DateTime.Now, $"¡Bienvenido al equipo, {n}!", "Recibimos tu registro. Ahora estamos revisando tus documentos para activar tu cuenta.", "hero_revision"));

            sb.Append(Seccion($"""
                {Titulo("Tu progreso")}
                {Paso("check", "Cuenta creada", "Tus datos y documentos llegaron bien.", hecho: true)}
                {Paso("pendiente", "Documentos en revisión", "Nuestro equipo está verificando tu identidad y antecedentes.", actual: true)}
                {Paso("paso", "Cuenta aprobada", "Te avisaremos en la app en cuanto quede lista.")}
                {Paso("paso", "Empieza a trabajar", "Actívate como disponible y recibe solicitudes cerca de ti.", ultimo: true)}
                """));

            sb.Append(Seccion($"""
                <tr><td colspan="2">{Destacado("actividad", "Sigue el estado en la app", "Entra a CuidApp con tu correo y verás el estado de cada documento: en revisión, aprobado o rechazado. Si alguno se rechaza, verás el motivo y podrás subir otro desde ahí mismo.")}</td></tr>
                """));

            sb.Append(Seccion($"""
                {Tarjetas(
                    ("escudo", "¿Por qué revisamos?", "Las familias confían en CuidApp porque cada cuidador está verificado."),
                    ("soporte", "¿Tienes dudas?", "Responde a este correo y te ayudamos con tu registro."))}
                """, fondo: Fondo));

            sb.Append(Pie("Recibiste este correo porque te registraste como cuidador en CuidApp."));
            return Envolver("Bienvenido al equipo de CuidApp", sb.ToString());
        }

        // Paso de la línea de progreso: ícono + línea vertical que lo une con el siguiente.
        private string Paso(string icono, string titulo, string texto, bool hecho = false, bool actual = false, bool ultimo = false)
        {
            var colorTitulo = hecho ? Verde : actual ? "#B26A00" : Gris;
            var linea = ultimo ? "" : $"""<div style="width:2px;height:26px;background:{(hecho ? Verde : Borde)};margin:4px auto 0"></div>""";
            return $"""
                <tr><td colspan="2">
                  <table role="presentation" width="100%" cellpadding="0" cellspacing="0"><tr>
                    <td width="28" style="vertical-align:top;padding-top:2px">{Icono(icono, 26)}{linea}</td>
                    <td style="vertical-align:top;padding:2px 0 {(ultimo ? 0 : 10)}px 12px">
                      <div style="font:700 15px Arial,Helvetica,sans-serif;color:{(hecho || actual ? Texto : Gris)}">{titulo}{(actual ? $" <span style=\"font:700 11px Arial,Helvetica,sans-serif;color:{colorTitulo};background:#FFF4DC;border-radius:8px;padding:2px 8px;margin-left:6px\">EN CURSO</span>" : "")}</div>
                      <div style="font:13px/19px Arial,Helvetica,sans-serif;color:{(hecho || actual ? Suave : Gris)};padding-top:2px">{texto}</div>
                    </td>
                  </tr></table>
                </td></tr>
                """;
        }

        // Fila de paso o consejo: ícono + título en negrita + descripción normal.
        private string Punto(string icono, string titulo, string texto) => $"""
            <tr><td colspan="2" style="padding:8px 0">
              <table role="presentation" cellpadding="0" cellspacing="0"><tr>
                <td style="vertical-align:top;padding-right:12px">{Icono(icono, 36)}</td>
                <td style="vertical-align:middle">
                  <div style="font:700 14px Arial,Helvetica,sans-serif;color:{Texto}">{titulo}</div>
                  <div style="font:13px/19px Arial,Helvetica,sans-serif;color:{Suave};padding-top:2px">{texto}</div>
                </td>
              </tr></table>
            </td></tr>
            """;

        // Caja destacada en azul suave (mensaje clave del correo).
        private string Destacado(string icono, string titulo, string texto) => $"""
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:#EAF1FB;border-radius:14px"><tr>
              <td width="48" style="padding:16px 0 16px 16px;vertical-align:top">{Icono(icono, 36)}</td>
              <td style="padding:16px">
                <div style="font:700 15px Arial,Helvetica,sans-serif;color:{Azul}">{titulo}</div>
                <div style="font:13px/20px Arial,Helvetica,sans-serif;color:{Texto};padding-top:4px">{texto}</div>
              </td>
            </tr></table>
            """;
    }
}
