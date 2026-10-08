using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace CUIDAPP_API.Seguridad
{
    /// <summary>
    /// Validación del JWT y permisos del panel administrativo. Solo los endpoints marcados con
    /// [Authorize(Policy = ...)] exigen token: la app móvil todavía no envía el suyo, así que el
    /// resto de la API sigue abierta como antes.
    /// Niveles de administrador (Usuarios.NivelAdmin): 1 Superadmin, 2 Operaciones/soporte, 3 Finanzas.
    /// </summary>
    public static class PoliticasAdmin
    {
        public const string Admin = "Admin";                 // cualquier administrador
        public const string Operaciones = "AdminOperaciones"; // superadmin u operaciones
        public const string Finanzas = "AdminFinanzas";       // superadmin o finanzas
        public const string SuperAdmin = "SuperAdmin";

        public const string ClaimNivel = "nivel_admin";

        public static string ClaveJwt(IConfiguration config)
            => config["Jwt:Key"] ?? "TuSuperClaveSecretaMuyLargaParaQueSeaSegura123!";

        public static int AdminId(ClaimsPrincipal user)
            => int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub"), out var id) ? id : 0;

        public static int Nivel(ClaimsPrincipal user)
            => int.TryParse(user.FindFirstValue(ClaimNivel), out var n) ? n : 0;

        private static bool EsAdmin(ClaimsPrincipal user) => user.IsInRole("1");

        public static IServiceCollection AddSeguridadAdmin(this IServiceCollection services, IConfiguration config)
        {
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(o =>
                {
                    o.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = config["Jwt:Issuer"] ?? "Cuidapp",
                        ValidateAudience = true,
                        ValidAudience = config["Jwt:Audience"] ?? "CuidappApp",
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.FromMinutes(2),
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(ClaveJwt(config))),
                        RoleClaimType = ClaimTypes.Role,
                        NameClaimType = ClaimTypes.Email
                    };
                });

            services.AddAuthorization(o =>
            {
                o.AddPolicy(Admin, p => p.RequireAuthenticatedUser().RequireAssertion(c => EsAdmin(c.User)));
                o.AddPolicy(Operaciones, p => p.RequireAuthenticatedUser().RequireAssertion(c => EsAdmin(c.User) && Nivel(c.User) is 1 or 2));
                o.AddPolicy(Finanzas, p => p.RequireAuthenticatedUser().RequireAssertion(c => EsAdmin(c.User) && Nivel(c.User) is 1 or 3));
                o.AddPolicy(SuperAdmin, p => p.RequireAuthenticatedUser().RequireAssertion(c => EsAdmin(c.User) && Nivel(c.User) == 1));
            });

            return services;
        }
    }
}
