using Ripple.NET;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRipple();

var app = builder.Build();

app.MapRippleDashboard();
app.MapGet("/", () => Results.Redirect("/ripple"));

app.Run();
