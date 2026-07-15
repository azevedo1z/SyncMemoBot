<p align="center">
  <img src="assets/syncmemo-avatar-compressed.jpeg" alt="SyncMemo" width="25%"/>
</p>

<h1 align="center">🇸🇾🇳🇨🇲🇪🇲🇴</h1>

<p align="center">
  <img src="https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet" alt=".NET 8"/>
  <img src="https://img.shields.io/badge/Discord.Net-3.19-5865F2?logo=discord" alt="Discord.Net"/>
  <img src="https://img.shields.io/badge/Hangfire-1.8.23-EE6F22" alt="Hangfire"/>
  <img src="https://img.shields.io/badge/Tests-10%20passing-2EAD33" alt="Tests"/>
</p>

---

## See it in action

You type:
```
/remindme  time: in 2 hours  message: pick up the laundry
```

Bot replies (only you can see):
> **SyncMemo** ·
> Got it. I'll DM you at **17:42 (UTC-3)**.

Two hours later, in your DMs:
> **SyncMemo** · today at 17:42
> Reminder: pick up the laundry

Forgetful friend? Remind *them* instead:
```
/remindsomeone  user: @ana  time: friday at 9am  message: submit the form
```

Friday at 9, Ana gets a DM; **⏰ @you: submit the form**; and knows exactly who to blame.

Want everyone in the group to know? Same thing, but in a channel:
```
/remindchannel  channel: #plans  time: amanhã às 20:00  message: movie night
```

At 8 PM tomorrow, `#plans` gets pinged. Nobody forgets. The night happens.

## Why this exists

Group chats drift. People schedule things in messages that scroll away in five minutes. Somebody has to be the one who remembers. That somebody is now a small .NET process running in the background.

## It speaks your language

The parser reads your Discord locale and tries that language first (Portuguese or English), falling back to the other if the phrase doesn't match.

| You write... | In | And the bot reads... |
|---|---|---|
| `in 2 hours` | EN | 2 hours from now |
| `tomorrow at 3pm` | EN | tomorrow, 15:00 |
| `next monday` | EN | upcoming Monday, 09:00 |
| `amanhã às 15:00` | PT | tomorrow, 15:00 |
| `daqui a 30 minutos` | PT | 30 minutes from now |
| `depois de amanhã` | PT | day after tomorrow, 09:00 |

> **Gotcha:** `15h`, `15:00`, and `15 horas` all parse to 15:00 and work fine. The traps are `em 15h` / `in 15h` (read as "+15 hours from now", not 15:00) and `15hs` with a trailing `s` (read as a duration). When in doubt, write the time explicitly: `15:00`.

## Get it running in 3 steps

**1. Make the bot in Discord.** Go to the [Developer Portal](https://discord.com/developers/applications), create a new application called `SyncMemo`. Open the **Bot** tab, hit `Reset Token`, copy it.

**2. Add the bot to your server.** Go to **OAuth2 → URL Generator**. Check `bot` and `applications.commands` under scopes, then `Send Messages` and `View Channels` under bot permissions. Copy the generated URL, open in a browser, pick the server, click `Authorize`.

**3. Run it locally.**
```powershell
git clone https://github.com/azevedo1z/syncmemobot.git
cd syncmemobot\src\SyncMemoBot.Discord
dotnet user-secrets set "Discord:Token" "<YOUR_TOKEN>"
dotnet run
```

The bot connects, registers `/remindme`, `/remindsomeone`, and `/remindchannel`, and exposes the Hangfire dashboard at `https://localhost:7204/hangfire`.

> No token configured? The app still boots in "dashboard only" mode, so you can poke at the Hangfire UI without a real bot.

## What it's made of

Three projects, dependencies flow one way: `Discord → Infrastructure → Core`.

| Project | Does what |
|---|---|
| [`SyncMemoBot.Core`](src/SyncMemoBot.Core) | Pure domain. Interfaces, sum types (`ReminderTarget`, `TimeParseResult`), options. Zero external deps. |
| [`SyncMemoBot.Infrastructure`](src/SyncMemoBot.Infrastructure) | The multilingual parser, the Hangfire scheduler, the localized strings, the DI extension. |
| [`SyncMemoBot.Discord`](src/SyncMemoBot.Discord) | Web host. Slash command modules, dispatcher, lifecycle, Hangfire dashboard. |

| Piece | Why it's here |
|---|---|
| `Discord.Net 3.19` | Gateway connection and slash commands |
| `Hangfire.Storage.SQLite` | Schedules survive restarts, no external DB needed |
| `Microsoft.Recognizers 1.8.13` | Natural language time parsing across PT and EN |
| `Serilog` | Rolling daily log files in `logs/bot-<date>.log` |
| `WebApplication` (Minimal APIs) | Hosts the Hangfire dashboard at `/hangfire` |

## Tweak the config

`appsettings.json`:
```json
{
  "Discord": { "Token": "", "DevGuildId": null },
  "Reminder": { "TimeZone": "America/Sao_Paulo" }
}
```

Running it from Lisbon? `"TimeZone": "Europe/Lisbon"`. Any IANA zone works.

Want slash commands to register instantly on a single test server (instead of waiting up to an hour for global propagation)?
```powershell
dotnet user-secrets set "Discord:DevGuildId" "<YOUR_GUILD_ID>"
```

In production, prefer env vars: `Discord__Token`, `Discord__DevGuildId`.

### Rate limiting

Each user is capped at **5 reminders/minute**, **50/day**, and **10/day toward any single other person** (via `/remindsomeone`, so the bot can't be turned into a DM-spam cannon). Tune it under a `RateLimit` section:
```json
{
  "RateLimit": {
    "BurstLimit": 5,  "BurstWindow": "00:01:00",
    "DailyLimit": 50, "DailyWindow": "1.00:00:00",
    "PerTargetDailyLimit": 10
  }
}
```
Counters live in their own `reminders.db`, separate from the Hangfire store. **In production, point both SQLite files at a persistent path** so schedules and quotas survive deploys:
```
ConnectionStrings__Hangfire=/var/lib/syncmemobot/hangfire.db
ConnectionStrings__RateLimit=/var/lib/syncmemobot/reminders.db
```

## Tests

```powershell
dotnet test
```

10 scenarios on the multilingual parser: PT, EN, locale fallback, unsupported locale, past time, garbage input, empty input. All pass, zero warnings.

---

<p align="center">
  <sub>Built for friends who keep saying "I forgot."</sub>
  <br/>
  <sub>Made with .NET and a healthy distrust of human memory.</sub>
</p>
