using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using SciFiCharacterChat.Components;
using SciFiCharacterChat.Data;
using SciFiCharacterChat.Services;
using System.ClientModel;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddHttpContextAccessor();

builder.Services.AddDbContextFactory<ChatDbContext>(options =>
    options.UseSqlite("Data Source=chat.db"));

var githubToken = builder.Configuration["GitHubModels:Token"]
    ?? throw new InvalidOperationException("Missing token — run: dotnet user-secrets set GitHubModels:Token <token>");

builder.Services.AddSingleton<IChatClient>(_ =>
    new ChatClient(
            "openai/gpt-4o-mini",
            new ApiKeyCredential(githubToken),
            new OpenAIClientOptions { Endpoint = new Uri("https://models.github.ai/inference") })
        .AsIChatClient());

builder.Services.AddScoped<VisitorContext>();
builder.Services.AddScoped<CharacterChatService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ChatDbContext>>();
    await using var db = await factory.CreateDbContextAsync();
    await db.Database.EnsureCreatedAsync();
}

app.Use(async (context, next) =>
{
    if (!context.Request.Cookies.TryGetValue("visitor_id", out var visitorId) || string.IsNullOrEmpty(visitorId))
    {
        visitorId = Guid.NewGuid().ToString("N");
        context.Response.Cookies.Append("visitor_id", visitorId, new CookieOptions
        {
            Expires = DateTimeOffset.UtcNow.AddYears(1),
            HttpOnly = true,
            IsEssential = true,
            SameSite = SameSiteMode.Lax
        });
    }
    context.Items["VisitorId"] = visitorId;
    await next();
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();