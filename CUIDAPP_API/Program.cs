using Scalar.AspNetCore;
using CUIDAPP_API.Interfaces.Auth;
using CUIDAPP_API.Services.Auth;
using CUIDAPP_API.Interfaces.Admin;
using CUIDAPP_API.Services.Admin;
using CUIDAPP_API.Interfaces.Cuidador;
using CUIDAPP_API.Services.Cuidador;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Inyección de dependencias
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<ICuidadorService, CuidadorService>();

var app = builder.Build();

// Habilitar documentación interactiva visual (Scalar UI)
app.MapOpenApi();
app.MapScalarApiReference();

// Redirigir la raíz (http://192.169.179.217/) directamente a la interfaz visual Scalar
app.MapGet("/", () => Results.Redirect("/scalar/v1"));

app.UseAuthorization();

app.MapControllers();

app.Run();
