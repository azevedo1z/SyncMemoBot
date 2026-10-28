# SyncMemoBot

Source code for a Discord bot that schedules reminders through slash commands. Reminder times are written in natural language (English or Portuguese) and delivered by direct message or in a server channel.

![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)
![Discord.Net](https://img.shields.io/badge/Discord.Net-3.19-5865F2?logo=discord)
![Hangfire](https://img.shields.io/badge/Hangfire-1.8.23-EE6F22)

## Contents

- [Repository structure](#repository-structure)
- [Requirements](#requirements)
- [Build and test](#build-and-test)
- [Local development](#local-development)
- [Configuration](#configuration)
- [Slash commands](#slash-commands)
- [Time input](#time-input)
- [Related documents](#related-documents)
- [License](#license)

## Repository structure

```
.
├── .github/workflows/dotnet.yml      CI: restore, build and test on push/PR to master
├── assets/                           Bot avatar and banner images
├── src/
│   ├── SyncMemoBot.Core/             Domain contracts, options and result types (no package dependencies)
│   ├── SyncMemoBot.Infrastructure/   Time parsing, Hangfire scheduling, SQLite rate limiting, localization, DI registration
│   └── SyncMemoBot.Discord/          ASP.NET Core host: Discord client, slash command module, dispatchers, Hangfire dashboard
├── tests/
│   └── SyncMemoBot.Tests/            xUnit tests for the multilingual time parser
├── SyncMemoBot.slnx                  Solution file
├── PRIVACY_POLICY.md
├── TERMS_OF_SERVICE.md
└── LICENSE.txt
```

Project references flow in one direction: `Discord → Infrastructure → Core`.

| Project | Main contents | Key packages |
|---|---|---|
| [`SyncMemoBot.Core`](src/SyncMemoBot.Core) | `IReminderScheduler`, `IReminderDispatcher`, `ITimeParsingService`, `IReminderRateLimiter`, `ReminderTarget`, `TimeParseResult`, `ReminderOptions`, `RateLimitOptions` | none |
| [`SyncMemoBot.Infrastructure`](src/SyncMemoBot.Infrastructure) | `MultilingualTimeParser`, `HangfireReminderScheduler`, `HangfireJobInvoker`, `SqliteReminderRateLimiter`, `LocalizedMessages`, `AddSyncMemoBotInfrastructure` | `Hangfire.Core`, `Hangfire.Storage.SQLite`, `Microsoft.Recognizers.Text.DateTime`, `sqlite-net-pcl` |
| [`SyncMemoBot.Discord`](src/SyncMemoBot.Discord) | `Program.cs`, `DiscordClientHost`, `ReminderCommandsModule`, `DiscordReminderDispatcher`, `DiscordReminderFailureNotifier` | `Discord.Net`, `Hangfire.AspNetCore`, `Serilog.AspNetCore` |
| [`SyncMemoBot.Tests`](tests/SyncMemoBot.Tests) | `MultilingualTimeParserTests` | `xunit`, `FluentAssertions` |

## Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- A Discord test application with a bot token (only needed to connect to Discord; see [Local development](#local-development))

No external database is required. Scheduled jobs and rate limit counters are stored in local SQLite files.

## Build and test

```bash
git clone https://github.com/azevedo1z/syncmemobot.git
cd syncmemobot
dotnet build
dotnet test
```

The same steps run in CI ([`.github/workflows/dotnet.yml`](.github/workflows/dotnet.yml)) on Ubuntu with .NET 8.0.x.

## Local development

These steps run the code for development and testing. Connecting to Discord requires a separate test application and a test server.

Without a token, the host starts without connecting to Discord and only the HTTP endpoints are available, which is enough to inspect scheduling through the Hangfire dashboard:

```bash
cd src/SyncMemoBot.Discord
dotnet run
```

To test the slash commands end to end:

1. In the [Discord Developer Portal](https://discord.com/developers/applications), create a test application, open the **Bot** tab and generate a token.
2. Under **OAuth2 → URL Generator**, select the `bot` and `applications.commands` scopes and the `Send Messages` and `View Channels` permissions. Use the generated URL to add the application to a test server.
3. Store the token and the test server ID with user secrets, then start the host:

```bash
cd src/SyncMemoBot.Discord
dotnet user-secrets set "Discord:Token" "<TOKEN>"
dotnet user-secrets set "Discord:DevGuildId" "<TEST_SERVER_ID>"
dotnet run
```

With `Discord:DevGuildId` set, the slash commands are registered to the test server only and are available immediately.

| Endpoint | Description |
|---|---|
| `/` | Plain-text health response |
| `/hangfire` | Hangfire dashboard, restricted to local requests |

Default URLs come from [`launchSettings.json`](src/SyncMemoBot.Discord/Properties/launchSettings.json): `http://localhost:5211` (`http` profile, used by `dotnet run`) and `https://localhost:7204` (`dotnet run --launch-profile https`).

## Configuration

Settings are read from [`appsettings.json`](src/SyncMemoBot.Discord/appsettings.json), user secrets and environment variables (use `__` as the section separator, e.g. `Discord__Token`).

| Key | Default | Description |
|---|---|---|
| `Discord:Token` | `""` | Bot token. Keep it out of `appsettings.json`. |
| `Discord:DevGuildId` | `null` | When set, commands are registered to this guild only (immediate). Otherwise they are registered globally, which can take up to an hour to propagate. |
| `Reminder:TimeZone` | `America/Sao_Paulo` | IANA time zone used to interpret and display reminder times. |
| `ConnectionStrings:Hangfire` | `hangfire.db` | SQLite file for Hangfire job storage. |
| `ConnectionStrings:RateLimit` | `reminders.db` | SQLite file for rate limit counters. |
| `RateLimit:BurstLimit` / `RateLimit:BurstWindow` | `5` / `00:01:00` | Reminders a user can create per burst window. |
| `RateLimit:DailyLimit` / `RateLimit:DailyWindow` | `50` / `1.00:00:00` | Reminders a user can create per daily window. |
| `RateLimit:PerTargetDailyLimit` | `10` | Daily reminders a user can send to the same person via `/remindsomeone`. |
| `Serilog:MinimumLevel` | see `appsettings*.json` | Log levels. |

For deployments, point both SQLite files to a persistent location so schedules and counters survive restarts:

```
ConnectionStrings__Hangfire=/var/lib/syncmemobot/hangfire.db
ConnectionStrings__RateLimit=/var/lib/syncmemobot/reminders.db
```

Logs are written to the console and to `logs/bot-<date>.log` (daily rolling, 7 files retained, 10 MB per file).

## Slash commands

Defined in [`ReminderCommandsModule.cs`](src/SyncMemoBot.Discord/Modules/ReminderCommandsModule.cs). Commands are available in servers only, and all responses are ephemeral.

| Command | Parameters | Delivery |
|---|---|---|
| `/remindme` | `time`, `message` | DM to the caller |
| `/remindsomeone` | `user`, `time`, `message` | DM to the selected user |
| `/remindchannel` | `channel`, `time`, `message` | Message in a text channel; both the caller and the bot need `Send Messages` there |

Limits: `time` up to 100 characters, `message` up to 1900 characters. Failed deliveries are retried by Hangfire up to 3 times; if the last retry fails, the reminder creator is notified by DM.

## Time input

[`MultilingualTimeParser`](src/SyncMemoBot.Infrastructure/Time/MultilingualTimeParser.cs) uses Microsoft.Recognizers.Text. The language matching the user's Discord locale is tried first (`pt*` maps to Portuguese, any other locale to English), then the other language.

- Relative and absolute expressions are supported, e.g. `in 2 hours`, `tomorrow at 3pm`, `em 2 horas`, `amanhã às 15:00`.
- Date-only expressions resolve to 09:00 in the configured time zone.
- Times that resolve to the past are rejected.
- `em 15h` / `in 15h` is read as a duration (15 hours from now), not 15:00. Use `15:00` for an explicit time.

## Related documents

- [Privacy Policy](PRIVACY_POLICY.md)
- [Terms of Service](TERMS_OF_SERVICE.md)

Both documents apply to the SyncMemoBot instance operated by the repository owner. They do not cover instances run by others from this source code.

## License

MIT. See [LICENSE.txt](LICENSE.txt).
