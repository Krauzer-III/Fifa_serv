using Fifa_serv.Data;
using Fifa_serv.Services;

var builder = WebApplication.CreateBuilder(args);

// Добавляем сервисы
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<ParserService>();

// Регистрируем наши сервисы
builder.Services.AddSingleton<LiteDbContext>();
builder.Services.AddScoped<HashService>();

// CORS для Android приложения
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Настройки
app.UseCors("AllowAll");
app.UseSwagger();
app.UseSwaggerUI();
app.UseStaticFiles();
app.MapControllers();

// Манифест с хешами
app.MapGet("/api/v1/manifest", (LiteDbContext db, HashService hash) =>
{
    var manifest = new
    {
        players = hash.ComputeHashFromList(db.Players.FindAll()),
        matches = hash.ComputeHashFromList(db.Matches.FindAll()),
        news = hash.ComputeHashFromList(db.News.FindAll()),
        club = db.ClubInfo.FindById(1) != null ? hash.ComputeHash(db.ClubInfo.FindById(1)) : ""
    };
    return Results.Ok(manifest);
});

// Тестовый эндпоинт
app.MapGet("/api/v1/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.Now }));

app.Run();