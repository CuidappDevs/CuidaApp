using Scalar.AspNetCore;
using CUIDAPP_API.Interfaces.Auth;
using CUIDAPP_API.Services.Auth;
using CUIDAPP_API.Interfaces.Admin;
using CUIDAPP_API.Services.Admin;
using CUIDAPP_API.Interfaces.Cuidador;
using CUIDAPP_API.Services.Cuidador;
using CUIDAPP_API.Interfaces.Trabajo;
using CUIDAPP_API.Services.Trabajo;
using CUIDAPP_API.Interfaces.Busqueda;
using CUIDAPP_API.Services.Busqueda;
using CUIDAPP_API.Interfaces.Cliente;
using CUIDAPP_API.Services.Cliente;
using CUIDAPP_API.Interfaces.Calificacion;
using CUIDAPP_API.Services.Calificacion;
using CUIDAPP_API.Interfaces.UbicacionCliente;
using CUIDAPP_API.Services.UbicacionCliente;
using CUIDAPP_API.Interfaces.Chat;
using CUIDAPP_API.Services.Chat;
using CUIDAPP_API.Services.Realtime;
using CUIDAPP_API.Hubs;
using Microsoft.Extensions.FileProviders;

// Asegurar que exista la carpeta wwwroot/uploads ANTES de crear el builder,
// para que ASP.NET Core detecte el WebRootPath correctamente al iniciar.
Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads"));

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddSignalR();

// Inyección de dependencias
builder.Services.AddSingleton<ITrabajoNotifier, TrabajoNotifier>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<ICuidadorService, CuidadorService>();
builder.Services.AddScoped<ITrabajoService, TrabajoService>();
builder.Services.AddScoped<IBusquedaService, BusquedaService>();
builder.Services.AddScoped<IClienteService, ClienteService>();
builder.Services.AddScoped<ICalificacionService, CalificacionService>();
builder.Services.AddScoped<IUbicacionClienteService, UbicacionClienteService>();
builder.Services.AddScoped<IChatService, ChatService>();

var app = builder.Build();

// Habilitar documentación interactiva visual (Scalar UI)
app.MapOpenApi();
app.MapScalarApiReference();

// Redirigir la raíz (http://192.169.179.217/) directamente a la interfaz visual Scalar
app.MapGet("/", () => Results.Redirect("/scalar/v1"));

// Servir explícitamente wwwroot/uploads (fotos, cédulas, documentos subidos)
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(Path.Combine(app.Environment.ContentRootPath, "wwwroot")),
    RequestPath = ""
});

app.UseAuthorization();

app.MapControllers();
app.MapHub<TrabajoHub>("/hubs/trabajo");

app.Run();
