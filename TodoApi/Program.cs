using Microsoft.EntityFrameworkCore;
using TodoApi.Data;
using TodoApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Register Controller
builder.Services.AddControllers();

// Register Database
builder.Services.AddDbContext<TodoDbContext>(options =>
{
    options.UseSqlite("Data Source=todos.db");
});

// Register Service
builder.Services.AddScoped<ITodoService, TodoService>();

var app = builder.Build();

// Routing
app.MapControllers();

app.Run();

// Agar Program dapat diakses oleh project testing
public partial class Program { }