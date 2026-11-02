# Portal de clientes

Aplicación React, TypeScript y Vite para consultar el catálogo, gestionar el carrito y consultar compras y facturas propias.

## Desarrollo

```powershell
npm ci
npm run dev
```

El servidor local usa `http://localhost:5173`. La aplicación se comunica con ASP.NET Core por HTTP/JSON y usa `http://localhost:5184` como API predeterminada. Configurá `VITE_API_URL` antes de compilar si la API está en otra dirección.

## Google Places para direcciones

El formulario de registro usa Google Places Autocomplete para sugerir direcciones de Argentina y completar localidad y código postal cuando Google los proporciona. Si no se configura la clave, el campo de dirección sigue disponible para carga manual.

Definí `VITE_GOOGLE_MAPS_API_KEY` en el entorno de Vite antes de iniciar o compilar la app. La clave de Maps JavaScript se entrega al navegador: restringila en Google Cloud por los sitios permitidos (por ejemplo, `http://localhost:5173/*` durante desarrollo y el dominio publicado) y habilitá Maps JavaScript API y Places API (New). Google Cloud requiere un proyecto con facturación habilitada.
