using Discord.Interactions;
using Discord.WebSocket;
using Hangfire;
using Hangfire.Dashboard;
using Serilog;
using SyncMemoBot.Core.Dispatch;
using SyncMemoBot.Core.Options;
using SyncMemoBot.Discord;
using SyncMemoBot.Discord.Dispatch;
using SyncMemoBot.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, services, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/bot-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7,
        fileSizeLimitBytes: 10_000_000,
        rollOnFileSizeLimit: true));

builder.Services
    .Configure<ReminderOptions>(builder.Configuration.GetSection(ReminderOptions.SectionName))
    .Configure<DiscordOptions>(builder.Configuration.GetSection(DiscordOptions.SectionName))
    .Configure<RateLimitOptions>(builder.Configuration.GetSection(RateLimitOptions.SectionName));

builder.Services.AddSyncMemoBotInfrastructure(
    builder.Configuration.GetConnectionString("Hangfire") ?? "hangfire.db",
    builder.Configuration.GetConnectionString("RateLimit") ?? "reminders.db");

builder.Services.AddSingleton(new DiscordSocketConfig
{
    GatewayIntents = global::Discord.GatewayIntents.Guilds | global::Discord.GatewayIntents.DirectMessages,
    LogLevel = global::Discord.LogSeverity.Info
});
builder.Services.AddSingleton<DiscordSocketClient>();
builder.Services.AddSingleton(sp =>
    new InteractionService(
        sp.GetRequiredService<DiscordSocketClient>(),
        new InteractionServiceConfig { DefaultRunMode = RunMode.Sync }));
builder.Services.AddSingleton<DiscordReadinessSignal>();
builder.Services.AddSingleton<IReminderDispatcher, DiscordReminderDispatcher>();
builder.Services.AddSingleton<IReminderFailureNotifier, DiscordReminderFailureNotifier>();
builder.Services.AddHostedService<DiscordClientHost>();

var app = builder.Build();

app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = [new LocalRequestsOnlyAuthorizationFilter()]
});
app.MapGet("/", () => "SyncMemoBot is running. Hangfire dashboard at /hangfire");

app.Run();
