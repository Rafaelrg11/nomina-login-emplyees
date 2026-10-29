# Evidencia GA7-220501096-AA5-EV03 — Diseño y desarrollo de servicios web

**Proyecto:** nomina-app  
**Aprendiz:** Rafael Antonio Rodriguez Polo  
**Tecnología:** ASP.NET Core 8, C#, REST, Swagger/OpenAPI.

## Contenido
- `nomina-api/`: código fuente de la API.
- `documentacion/`: documentación funcional y técnica de los endpoints.
- `ENLACE_REPOSITORIO.txt`: espacio para pegar la URL real del repositorio Git.

## Requisitos
- .NET SDK 8.0 o superior.
- Git.
- Navegador web para consultar Swagger.

## Ejecutar localmente
Desde la carpeta `nomina-api`:

```bash
dotnet restore
dotnet run
```

La API se inicia en `http://127.0.0.1:5081`. Documentación interactiva: `http://127.0.0.1:5081/swagger`.

## Servicios incluidos
- Sistema: `GET /`, `GET /api/health`.
- Autenticación: `POST /api/auth/register`, `POST /api/auth/login`.
- Empleados: `GET /api/employees`, `GET /api/employees/{id}`, `POST /api/employees`, `PUT /api/employees/{id}`, `DELETE /api/employees/{id}`.
- Nómina: `GET /api/payroll`, `GET /api/payroll/{id}`, `POST /api/payroll`.

## Consideraciones
Esta versión académica utiliza almacenamiento en memoria para facilitar la ejecución sin instalar una base de datos. Los datos se reinician al detener la aplicación. El login devuelve un token de demostración; antes de usar en producción se debe implementar JWT firmado, autorización por roles, persistencia en base de datos, secretos gestionados por variables de entorno, pruebas automatizadas y controles adicionales de seguridad.
