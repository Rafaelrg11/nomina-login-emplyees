# Documentación rápida de endpoints

Base URL local: `http://127.0.0.1:5081`  
Swagger: `/swagger`

| Método | Ruta | Función |
|---|---|---|
| GET | `/` | Estado e información general |
| GET | `/api/health` | Comprobar disponibilidad |
| POST | `/api/auth/register` | Registrar usuario |
| POST | `/api/auth/login` | Iniciar sesión |
| GET | `/api/employees?skip=0&limit=10&search=` | Listar/buscar empleados |
| GET | `/api/employees/{id}` | Consultar empleado |
| POST | `/api/employees` | Crear empleado |
| PUT | `/api/employees/{id}` | Actualizar empleado |
| DELETE | `/api/employees/{id}` | Eliminar empleado |
| GET | `/api/payroll?skip=0&limit=10&year=2026&month=10` | Consultar nóminas |
| GET | `/api/payroll/{id}` | Consultar registro de nómina |
| POST | `/api/payroll` | Crear registro de nómina |

Los campos de los endpoints POST están descritos en el documento DOCX y en Swagger. Los datos viven en memoria y se pierden al reiniciar el proceso.
