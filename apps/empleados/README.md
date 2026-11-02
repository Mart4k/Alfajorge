# Portal de empleados

Aplicación React, TypeScript y Vite para el personal de Alfajorge. Los permisos de las cuentas `VENDEDOR` y `ADMINISTRATIVO` se validan en el backend.

## Desarrollo

```powershell
npm ci
npm run dev
```

El servidor local usa `http://localhost:5174`. La aplicación se comunica con ASP.NET Core por HTTP/JSON y usa `http://localhost:5184` como API predeterminada. Configurá `VITE_API_URL` antes de compilar si la API está en otra dirección.
