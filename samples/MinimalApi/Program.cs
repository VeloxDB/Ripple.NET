using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// 1. Configure Services
builder.Services.AddOpenApi(); // Built-in OpenAPI support
builder.Services.AddDbContext<AppDbContext>(options => 
    options.UseSqlite("Data Source=app.db"));

var app = builder.Build();


// 2. Configure Pipeline
if (app.Environment.IsDevelopment())
{
    Console.WriteLine($"{app.Environment.IsDevelopment()} Development environment: Enabling OpenAPI and Scalar UI");

    app.MapOpenApi(); // Expose openapi.json
    app.MapScalarApiReference(); // Expose Scalar UI at /scalar/v1
}

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

// 3. Define Endpoints
var api = app.MapGroup("/items");

// Standard CRUD
api.MapGet("/", async (AppDbContext db) => await db.Items.ToListAsync());

api.MapGet("/{id}", async (int id, AppDbContext db) => 
    await db.Items.FindAsync(id) is Item item ? Results.Ok(item) : Results.NotFound());

api.MapPost("/", async (Item item, AppDbContext db) =>
{
    db.Items.Add(item);
    await db.SaveChangesAsync();
    return Results.Created($"/items/{item.Id}", item);
});

api.MapDelete("/{id}", async (int id, AppDbContext db) =>
{
    if (await db.Items.FindAsync(id) is Item item)
    {
        db.Items.Remove(item);
        await db.SaveChangesAsync();
        return Results.NoContent();
    }
    return Results.NotFound();
});

// --- SPECIAL: Dual-Transaction Endpoint ---
// This operation performs two separate database transactions in one API call.
// Use Case: We want to log an "attempt" that persists even if the main operation fails.
api.MapPost("/process-transaction/{id}", async (int id, AppDbContext db) =>
{
    // Transaction 1: Audit Log (Read/Write)
    // We start a transaction, write a log, and commit immediately.
    // This ensures the log exists even if the second part crashes.
    using (var transaction1 = await db.Database.BeginTransactionAsync())
    {
        try
        {
            db.Logs.Add(new AuditLog { Message = $"Processing started for Item {id} at {DateTime.Now}" });
            await db.SaveChangesAsync();
            await transaction1.CommitAsync(); 
        }
        catch 
        {
            // If logging fails, we might decide to abort everything
            return Results.Problem("Failed to create audit log.");
        }
    }

    // Transaction 2: Business Logic (Read/Write)
    // We start a NEW transaction for the actual data modification.
    using (var transaction2 = await db.Database.BeginTransactionAsync())
    {
        try
        {
            var item = await db.Items.FindAsync(id);
            if (item == null) 
            {
                // Note: Transaction 1 (Log) is ALREADY committed and will stay in the DB.
                return Results.NotFound("Item not found, but attempt was logged.");
            }

            // Modify the item
            item.Name += " [Processed]";
            item.LastProcessed = DateTime.Now;
            
            await db.SaveChangesAsync();
            await transaction2.CommitAsync();
            
            return Results.Ok(new { Message = "Success", Item = item });
        }
        catch
        {
            await transaction2.RollbackAsync();
            return Results.Problem("Error processing item.");
        }
    }
});

app.Run();

// 4. Models and DbContext
public class Item
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public DateTime? LastProcessed { get; set; }
}

public class AuditLog
{
    public int Id { get; set; }
    public required string Message { get; set; }
}

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) {}
    public DbSet<Item> Items => Set<Item>();
    public DbSet<AuditLog> Logs => Set<AuditLog>();
}