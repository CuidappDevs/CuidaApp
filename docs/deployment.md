# Publicar `CUIDAPP_API` desde Visual Studio (Web Deploy)

Antes, cada cambio se publicaba a una carpeta local y se copiaba manualmente al servidor por RDP. Ahora Visual Studio publica **directo** al servidor IIS con un clic (perfil `IISProfile.pubxml`).

## Requisitos del servidor (ya configurados, quedan documentados por si hay que rehacerlos)

1. **Web Deploy 4.0** instalado (`webdeploy_amd64_en-US.msi`, instalación completa).
2. **Característica de Windows "Management Service"** habilitada — es *aparte* del instalador de Web Deploy:
   - Server Manager → Add Roles and Features → Server Roles → Web Server (IIS) → Management Tools → **Management Service**.
   - **Importante**: si Web Deploy se instaló *antes* de habilitar esta característica, no queda bien enganchado (el endpoint `/msdeploy.axd` da 404). Hay que **desinstalar y reinstalar Web Deploy** después de habilitar Management Service — una simple "Reparar" no alcanza.
   - Si IIS Manager ya estaba abierto cuando se instaló la característica, hay que cerrarlo y reabrirlo para que aparezca el ícono "Management Service" (carga su lista de módulos solo al iniciar).
3. En **IIS Manager**, nodo raíz del servidor → **Management Service**:
   - "Enable remote connections" marcado.
   - Puerto **8172** (por defecto).
   - Aplicar y arrancar el servicio.
4. **Firewall**: regla de entrada para el puerto 8172 (Web Deploy la crea automáticamente al instalarse — verificar que exista y esté habilitada en "Windows Defender Firewall with Advanced Security" → Inbound Rules → "Web Management Service (HTTP Traffic-In)").

### Cómo diagnosticar si algo falla

Desde el propio servidor, en un navegador: `https://localhost:8172/msdeploy.axd`

- **404** → el endpoint no está registrado (ver paso 2, reinstalar Web Deploy).
- **Pide usuario/contraseña (401)** → todo bien configurado del lado del servidor; si Visual Studio sigue sin conectar, el problema es de red/firewall entre tu máquina y el servidor.

## Configuración del perfil en Visual Studio

Archivo: `CUIDAPP_API/Properties/PublishProfiles/IISProfile.pubxml`

```xml
<MSDeployServiceURL>192.169.179.217</MSDeployServiceURL>
<DeployIisAppPath>API_CUIDAPP</DeployIisAppPath>
<MSDeployPublishMethod>WMSVC</MSDeployPublishMethod>
<AllowUntrustedCertificate>True</AllowUntrustedCertificate>
<UserName>217-179-169-192\cuidappdev</UserName>
```

Puntos clave:
- **Servidor**: solo la IP (`192.169.179.217`), **sin** `:8172` — Visual Studio agrega el puerto solo. Ponerlo explícito causa un 404 (duplica la ruta).
- **Usuario**: formato completo `NOMBRE-DE-MAQUINA\usuario` (para una cuenta local de Windows) — en este servidor, el nombre de máquina es literalmente `217-179-169-192`. Se puede confirmar el formato exacto en IIS Manager → sitio → **IIS Manager Permissions**, columna "Name".
- **`AllowUntrustedCertificate`**: obligatorio porque el servidor usa un certificado SSL autofirmado (`WMSVC-SHA2`) para el Management Service. Sin esto, el publish falla con "Error de la tarea de implementación web" / "no se pudo comprobar el certificado del servidor" — aunque "Validar conexión" sí funcione (la validación de conexión y el publish real usan rutas de código distintas).

## Cómo publicar

1. Abrir el proyecto `CUIDAPP_API` en Visual Studio.
2. Click derecho en el proyecto → **Publicar** → seleccionar el perfil `IISProfile`.
3. Botón **Publicar**.

Listo — no hace falta tocar nada más salvo que cambie el usuario, la contraseña, o la IP del servidor.
