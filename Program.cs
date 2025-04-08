var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Register SMS Rate Limiter Service
builder.Services.AddSingleton<SmsRateLimiter.Services.ISmsRateLimiterService, SmsRateLimiter.Services.SmsRateLimiterService>();
builder.Services.AddHostedService<SmsRateLimiter.Services.SmsRateLimiterService>(
    provider => (SmsRateLimiter.Services.SmsRateLimiterService)provider.GetRequiredService<SmsRateLimiter.Services.ISmsRateLimiterService>()
);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Serve static files from wwwroot
app.UseStaticFiles();

app.UseAuthorization();

app.MapControllers();

// Fallback to index.html for SPA routing
app.MapFallbackToFile("index.html");

app.Run();
