using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Ripple.NET;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// 1. Configure Services
builder.Services.AddRipple();
builder.Services.AddOpenApi(); // Built-in OpenAPI support
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=app.db").UseRipple());

var app = builder.Build();


// 2. Configure Pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi(); // Expose openapi.json
    app.MapScalarApiReference(); // Expose Scalar UI at /scalar/v1
    app.UseRipple();
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

api.MapPost("/process-transaction/{id}", async (int id, AppDbContext db) =>
{
    using (var transaction1 = await db.Database.BeginTransactionAsync())
    {
        try
        {
            db.Logs.Add(new AuditLog { Message = $"Processing started for Item {id} at {DateTime.Now}", ItemId = id });
            await db.SaveChangesAsync();
            await transaction1.CommitAsync();
        }
        catch
        {
            return Results.Problem("Failed to create audit log.");
        }
    }

    using (var transaction2 = await db.Database.BeginTransactionAsync())
    {
        try
        {
            var item = await db.Items.FindAsync(id);
            if (item == null)
            {
                return Results.NotFound("Item not found, but attempt was logged.");
            }

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

api.MapGet("/with-logs", async (AppDbContext db) =>
{
    var query = from item in db.Items
                join log in db.Logs
                on item.Id equals log.Id
                select new
                {
                    ItemName = item.Name,
                    LogEntry = log.Message
                };

    return await query.ToListAsync();
});

app.Run();

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

    [ForeignKey("Item")]
    [Required]
    public int ItemId { get; set; }
}

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) {}
    public DbSet<Item> Items => Set<Item>();
    public DbSet<AuditLog> Logs => Set<AuditLog>();
}