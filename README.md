# Alfajorge

Sistema local de venta de alfajores formado por una API ASP.NET Core 10, una base SQLite y dos aplicaciones web independientes: clientes y personal.

## Requisitos

- .NET 10 SDK.
- Node.js 20.19+ o 22.12+ y npm.

La base se crea automáticamente en `%LOCALAPPDATA%\Alfajorge\alfajorge.db`. La clave de firma local se guarda en `%LOCALAPPDATA%\Alfajorge\jwt-signing.key` y se genera aleatoriamente la primera vez. Se pueden cambiar las ubicaciones con `ALFAJORGE_DATA_DIRECTORY` y `ALFAJORGE_DB_PATH`.

Al primer inicio se crean las tablas y los datos iniciales del catálogo (tipos, sabores, coberturas y presentaciones). No se crean alfajores de muestra ni credenciales predeterminadas. La antigua `backend/alfajorge.db` se conserva sin modificar porque contiene tablas que no corresponden a este sistema.

## Primer inicio y administrador

Para habilitar el portal interno, inicia el API y abre `http://localhost:5174`. Si todavía no existe ningún empleado, el portal mostrará **Configurar primer administrador**. Completá el formulario una sola vez; luego quedarás conectado y podrás crear el resto del personal desde el panel.

También podés crear la cuenta durante el primer inicio configurando estas variables de entorno en PowerShell:

```powershell
$env:ALFAJORGE_INITIAL_ADMIN_EMAIL = "tu-correo@ejemplo.com"
$securePassword = Read-Host "Contraseña del administrador (mínimo 10 caracteres)" -AsSecureString
$env:ALFAJORGE_INITIAL_ADMIN_PASSWORD = [Net.NetworkCredential]::new("", $securePassword).Password
cd backend
dotnet run --urls http://localhost:5184
```

La contraseña se guarda como hash; no se escribe en archivos del proyecto. La cuenta se crea una sola vez y la configuración se ignora cuando ya hay empleados. Al terminar el primer inicio, se pueden quitar las variables de la terminal:

```powershell
$env:ALFAJORGE_INITIAL_ADMIN_EMAIL = $null
$env:ALFAJORGE_INITIAL_ADMIN_PASSWORD = $null
```

La creación inicial desde el portal solo está permitida desde esta máquina y se bloquea en cuanto existe el primer empleado. No hay contraseñas predeterminadas ni registro público de administradores.

## Ejecutar las aplicaciones

Iniciar el API y, en otras dos terminales, cada frontend:

```powershell
cd backend
dotnet run --urls http://localhost:5184
```

```powershell
cd apps/alfajorge/clientes
npm.cmd ci
npm.cmd run dev
```

```powershell
cd apps/alfajorge/empleados
npm.cmd ci
npm.cmd run dev
```

- Clientes: `http://localhost:5173`
- Empleados: `http://localhost:5174`
- API y estado de la base: `http://localhost:5184/api/health`

Ambas apps usan `VITE_API_URL` para seleccionar el API; por defecto es `http://localhost:5184`. Los orígenes locales de Vite ya están permitidos por `appsettings.Development.json`.

## Datos y seguridad local

- SQLite es accesible únicamente a través del API; los navegadores nunca abren el archivo directamente.
- El API crea el esquema la primera vez y completa las listas iniciales del catálogo cuando están vacías. No borra datos existentes en la ubicación configurada.
- Hacé copias de seguridad del archivo `alfajorge.db` con el API detenido.
- Los secretos y datos locales están fuera del repositorio, dentro de `%LOCALAPPDATA%\Alfajorge`.
- La configuración está pensada para uso en esta máquina. No expongas el API en la red sin configurar autenticación, HTTPS y reglas de acceso adecuadas.

## Funcionalidad

- El portal de clientes permite registro, inicio de sesión, catálogo, compras y consulta de operaciones/facturas propias.
- El personal puede registrar ventas presenciales; los administrativos gestionan productos, stock, precios, promociones, clientes, empleados y operaciones.
- Los precios y el stock se validan en el API. El sistema no integra una pasarela de pago: los pagos externos quedan pendientes de confirmación administrativa.
- Las contraseñas nuevas usan `PasswordHasher` de ASP.NET Core Identity. Contraseñas antiguas con hash SHA-256 se actualizan gradualmente al iniciar sesión.

## Compilar y probar

```powershell
dotnet build backend/Alfajorge.Api.csproj
dotnet test tests/Alfajorge.Api.Tests/Alfajorge.Api.Tests.csproj
npm.cmd --prefix apps/alfajorge/clientes run build
npm.cmd --prefix apps/alfajorge/empleados run build
```

El endpoint `GET /api/health` informa si la base SQLite está disponible.

## Despliegue en Vercel

La raíz `vercel.json` configura un proyecto Vercel con ambos portales:

- `clientes`: `/clientes/*`
- `empleados`: `/empleados/*`

El build raíz usa `npm ci` y genera los dos portales dentro de `dist/clientes` y `dist/empleados`. No se agregaron bindings porque los portales no se comunican entre sí por API: los enlaces entre portales son URLs públicas.

Definí `VITE_API_URL` con la URL pública del backend. No uses `localhost` en un despliegue público.

El backend ASP.NET Core continúa separado en `backend/` y debe publicarse en un proveedor compatible con .NET, como Render, Railway o Azure. Luego agregá su URL pública como `VITE_API_URL` en Vercel. El backend permite los dominios principales de Vercel y acepta dominios adicionales mediante la variable `ALFAJORGE_ALLOWED_ORIGINS`, separados por comas.
