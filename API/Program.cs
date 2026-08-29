using API.ServiceRegister;

var builder = WebApplication.CreateBuilder(args);

var envFileCandidates = new[]
{
    Path.Combine(builder.Environment.ContentRootPath, ".env"),
    Path.Combine(builder.Environment.ContentRootPath, "API", ".env"),
    Path.Combine(AppContext.BaseDirectory, ".env")
};

var envFile = envFileCandidates.FirstOrDefault(File.Exists);

if (envFile is not null)
{
    var envValues = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

    foreach (var line in File.ReadLines(envFile))
    {
        var trimmedLine = line.Trim();

        if (trimmedLine.Length == 0 || trimmedLine.StartsWith('#'))
        {
            continue;
        }

        var separatorIndex = trimmedLine.IndexOf('=');

        if (separatorIndex > 0)
        {
            envValues[trimmedLine[..separatorIndex].Trim()] =
                trimmedLine[(separatorIndex + 1)..].Trim();
        }
    }

    builder.Configuration.AddInMemoryCollection(envValues);
}

builder.Configuration.AddEnvironmentVariables();

// Add services
builder.Services.AddService(builder.Configuration);

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowAnyOrigin();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors();

app.MapControllers();

app.Run();
