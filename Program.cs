using Microsoft.EntityFrameworkCore;
using Xllerate.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy.AllowAnyHeader().AllowAnyMethod().WithOrigins(builder.Configuration
    .GetSection("AllowedOrigins")
    .Get<string[]>() ?? [])));

builder.Services.AddControllers();
var app = builder.Build();

app.UseCors();

if (app.Environment.IsDevelopment()) {
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();