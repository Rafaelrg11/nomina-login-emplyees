using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy => policy
        .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
        .AllowAnyHeader()
        .AllowAnyMethod());
});

var app = builder.Build();
app.UseCors("Frontend");
app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/", () => Results.Ok(new
{
    application = "Nomina API",
    version = "1.0.0",
    documentation = "/swagger",
    status = "running"
})).WithTags("Sistema").WithSummary("Información general de la API");

app.MapGet("/api/health", () => Results.Ok(new { status = "healthy", timestamp = DateTimeOffset.UtcNow }))
    .WithTags("Sistema").WithSummary("Verifica la disponibilidad del servicio");

// Demo storage: data is kept in memory and is reset when the application restarts.
var users = new ConcurrentDictionary<string, UserRecord>(StringComparer.OrdinalIgnoreCase);
var employees = new ConcurrentDictionary<Guid, Employee>();
var payrolls = new ConcurrentDictionary<Guid, PayrollRecord>();

// Authentication endpoints for the nomina-app frontend.
app.MapPost("/api/auth/register", (RegisterRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Email) ||
        string.IsNullOrWhiteSpace(request.Password))
        return Results.BadRequest(ApiResponse<object>.Fail("Nombre, correo y contraseña son obligatorios.", 400));

    if (!new EmailAddressAttribute().IsValid(request.Email))
        return Results.BadRequest(ApiResponse<object>.Fail("El correo electrónico no tiene un formato válido.", 400));

    if (request.Password.Length < 8)
        return Results.BadRequest(ApiResponse<object>.Fail("La contraseña debe tener al menos 8 caracteres.", 400));

    var key = request.Email.Trim();
    var record = new UserRecord(Guid.NewGuid(), request.Name.Trim(), key, HashPassword(request.Password));
    if (!users.TryAdd(key, record))
        return Results.Conflict(ApiResponse<object>.Fail("Ya existe una cuenta con ese correo.", 409));

    return Results.Created($"/api/auth/users/{record.Id}",
        ApiResponse<object>.Ok(new { record.Id, record.Name, record.Email }, "Usuario registrado correctamente."));
})
.WithTags("Autenticación").WithSummary("Registra un usuario")
.Produces<ApiResponse<object>>(201).Produces<ApiResponse<object>>(400).Produces<ApiResponse<object>>(409);

app.MapPost("/api/auth/login", (LoginRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        return Results.BadRequest(ApiResponse<object>.Fail("Correo y contraseña son obligatorios.", 400));

    if (!users.TryGetValue(request.Email.Trim(), out var user) ||
        !VerifyPassword(request.Password, user.PasswordHash))
        return Results.Unauthorized();

    // Demo-only session token. Replace with signed JWT and persistent identity in production.
    var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    return Results.Ok(ApiResponse<object>.Ok(new
    {
        token,
        user = new { user.Id, user.Name, user.Email },
        expiresInMinutes = 60
    }, "Inicio de sesión exitoso."));
})
.WithTags("Autenticación").WithSummary("Valida credenciales e inicia sesión")
.Produces<ApiResponse<object>>(200).Produces(400).Produces(401);

// Employee CRUD endpoints.
app.MapGet("/api/employees", (int? skip, int? limit, string? search) =>
{
    var safeSkip = Math.Max(0, skip ?? 0);
    var safeLimit = Math.Clamp(limit ?? 10, 1, 100);
    var query = employees.Values.AsEnumerable();
    if (!string.IsNullOrWhiteSpace(search))
        query = query.Where(e => e.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                                 e.DocumentNumber.Contains(search, StringComparison.OrdinalIgnoreCase));
    var all = query.OrderBy(e => e.Name).ToList();
    return Results.Ok(ApiResponse<object>.Ok(new
    {
        items = all.Skip(safeSkip).Take(safeLimit),
        totalCount = all.Count,
        skip = safeSkip,
        limit = safeLimit
    }));
}).WithTags("Empleados").WithSummary("Lista empleados con paginación y búsqueda");

app.MapGet("/api/employees/{id:guid}", (Guid id) =>
    employees.TryGetValue(id, out var employee)
        ? Results.Ok(ApiResponse<Employee>.Ok(employee))
        : Results.NotFound(ApiResponse<object>.Fail("No se encontró el empleado.", 404)))
    .WithTags("Empleados").WithSummary("Consulta un empleado por identificador");

app.MapPost("/api/employees", (EmployeeRequest request) =>
{
    var validation = ValidateEmployee(request);
    if (validation is not null) return Results.BadRequest(ApiResponse<object>.Fail(validation, 400));

    if (employees.Values.Any(e => e.DocumentNumber.Equals(request.DocumentNumber.Trim(), StringComparison.OrdinalIgnoreCase)))
        return Results.Conflict(ApiResponse<object>.Fail("Ya existe un empleado con ese documento.", 409));

    var employee = new Employee(Guid.NewGuid(), request.Name.Trim(), request.DocumentNumber.Trim(),
        request.Email.Trim(), request.Position.Trim(), request.Salary);
    employees[employee.Id] = employee;
    return Results.Created($"/api/employees/{employee.Id}",
        ApiResponse<Employee>.Ok(employee, "Empleado creado correctamente."));
}).WithTags("Empleados").WithSummary("Crea un empleado")
.Produces<ApiResponse<Employee>>(201).Produces<ApiResponse<object>>(400).Produces<ApiResponse<object>>(409);

app.MapPut("/api/employees/{id:guid}", (Guid id, EmployeeRequest request) =>
{
    if (!employees.ContainsKey(id)) return Results.NotFound(ApiResponse<object>.Fail("No se encontró el empleado.", 404));
    var validation = ValidateEmployee(request);
    if (validation is not null) return Results.BadRequest(ApiResponse<object>.Fail(validation, 400));

    if (employees.Values.Any(e => e.Id != id && e.DocumentNumber.Equals(request.DocumentNumber.Trim(), StringComparison.OrdinalIgnoreCase)))
        return Results.Conflict(ApiResponse<object>.Fail("Otro empleado ya usa ese documento.", 409));

    var updated = new Employee(id, request.Name.Trim(), request.DocumentNumber.Trim(),
        request.Email.Trim(), request.Position.Trim(), request.Salary);
    employees[id] = updated;
    return Results.Ok(ApiResponse<Employee>.Ok(updated, "Empleado actualizado correctamente."));
}).WithTags("Empleados").WithSummary("Actualiza un empleado")
.Produces<ApiResponse<Employee>>(200).Produces<ApiResponse<object>>(400).Produces<ApiResponse<object>>(404);

app.MapDelete("/api/employees/{id:guid}", (Guid id) =>
    employees.TryRemove(id, out _)
        ? Results.Ok(ApiResponse<object>.Ok(new { id }, "Empleado eliminado correctamente."))
        : Results.NotFound(ApiResponse<object>.Fail("No se encontró el empleado.", 404)))
    .WithTags("Empleados").WithSummary("Elimina un empleado");

// Payroll CRUD/read endpoints.
app.MapGet("/api/payroll", (int? skip, int? limit, int? year, int? month) =>
{
    var safeSkip = Math.Max(0, skip ?? 0);
    var safeLimit = Math.Clamp(limit ?? 10, 1, 100);
    var query = payrolls.Values.AsEnumerable();
    if (year.HasValue) query = query.Where(p => p.Year == year.Value);
    if (month.HasValue) query = query.Where(p => p.Month == month.Value);
    var all = query.OrderByDescending(p => p.Year).ThenByDescending(p => p.Month).ToList();
    return Results.Ok(ApiResponse<object>.Ok(new
    {
        items = all.Skip(safeSkip).Take(safeLimit),
        totalCount = all.Count,
        skip = safeSkip,
        limit = safeLimit
    }));
}).WithTags("Nómina").WithSummary("Consulta registros de nómina con filtros y paginación");

app.MapGet("/api/payroll/{id:guid}", (Guid id) =>
    payrolls.TryGetValue(id, out var payroll)
        ? Results.Ok(ApiResponse<PayrollRecord>.Ok(payroll))
        : Results.NotFound(ApiResponse<object>.Fail("No se encontró el registro de nómina.", 404)))
    .WithTags("Nómina").WithSummary("Consulta un registro de nómina");

app.MapPost("/api/payroll", (PayrollRequest request) =>
{
    if (request.EmployeeId == Guid.Empty || request.Year < 2000 || request.Month is < 1 or > 12 ||
        request.BaseSalary < 0 || request.Deductions < 0 || request.Bonuses < 0)
        return Results.BadRequest(ApiResponse<object>.Fail("Verifique empleado, periodo y valores monetarios.", 400));

    if (!employees.ContainsKey(request.EmployeeId))
        return Results.BadRequest(ApiResponse<object>.Fail("El empleado indicado no existe.", 400));

    if (payrolls.Values.Any(p => p.EmployeeId == request.EmployeeId && p.Year == request.Year && p.Month == request.Month))
        return Results.Conflict(ApiResponse<object>.Fail("Ya existe nómina para ese empleado y periodo.", 409));

    var record = new PayrollRecord(Guid.NewGuid(), request.EmployeeId, request.Year, request.Month,
        request.BaseSalary, request.Bonuses, request.Deductions,
        request.BaseSalary + request.Bonuses - request.Deductions, DateTimeOffset.UtcNow);
    payrolls[record.Id] = record;
    return Results.Created($"/api/payroll/{record.Id}",
        ApiResponse<PayrollRecord>.Ok(record, "Registro de nómina creado correctamente."));
}).WithTags("Nómina").WithSummary("Crea un registro de nómina")
.Produces<ApiResponse<PayrollRecord>>(201).Produces<ApiResponse<object>>(400).Produces<ApiResponse<object>>(409);

app.Run();

static string? ValidateEmployee(EmployeeRequest request)
{
    if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.DocumentNumber) ||
        string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Position))
        return "Nombre, documento, correo y cargo son obligatorios.";
    if (!new EmailAddressAttribute().IsValid(request.Email)) return "El correo electrónico no es válido.";
    if (request.Salary < 0) return "El salario no puede ser negativo.";
    return null;
}

// PBKDF2 stores a salted password hash rather than the original password.
static string HashPassword(string password)
{
    var salt = RandomNumberGenerator.GetBytes(16);
    var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, 32);
    return $"{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
}
static bool VerifyPassword(string password, string stored)
{
    var parts = stored.Split(':');
    if (parts.Length != 2) return false;
    var salt = Convert.FromBase64String(parts[0]);
    var expected = Convert.FromBase64String(parts[1]);
    var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, expected.Length);
    return CryptographicOperations.FixedTimeEquals(actual, expected);
}

record RegisterRequest(string Name, string Email, string Password);
record LoginRequest(string Email, string Password);
record EmployeeRequest(string Name, string DocumentNumber, string Email, string Position, decimal Salary);
record Employee(Guid Id, string Name, string DocumentNumber, string Email, string Position, decimal Salary);
record PayrollRequest(Guid EmployeeId, int Year, int Month, decimal BaseSalary, decimal Bonuses, decimal Deductions);
record PayrollRecord(Guid Id, Guid EmployeeId, int Year, int Month, decimal BaseSalary, decimal Bonuses,
    decimal Deductions, decimal NetPay, DateTimeOffset CreatedAt);
record UserRecord(Guid Id, string Name, string Email, string PasswordHash);
record ApiResponse<T>(bool Success, string Message, int Code, T? Data)
{
    public static ApiResponse<T> Ok(T data, string message = "Operación exitosa.") => new(true, message, 200, data);
    public static ApiResponse<T> Fail(string message, int code) => new(false, message, code, default);
}
