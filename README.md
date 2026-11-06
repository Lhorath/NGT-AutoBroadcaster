# Nerdy Gamer Tools AutoBroadcaster

**Version 2.1.0** — Valheim **1.0.20** (network version 41)

A dedicated-server-only scheduled broadcast mod for Valheim. Vanilla clients do not need the mod installed, and the plugin does not add a custom network handshake or disable Valheim crossplay.

This repository preserves the source for the rebuilt AutoBroadcaster so future Valheim patches can be handled without losing the implementation again.

## Features

- Hourly scheduling by minute (`0-59`) for each message.
- Delivery type per message: `Alert`, `Chat`, or `Both`.
- Live config reload without restarting the dedicated server.
- Dedicated-server runtime guard. The plugin runs only when `ZNet` reports a dedicated server, so installing the DLL on a normal client or a listen host does nothing.
- No custom RPC registration, network handshake, prefab, asset bundle, or client dependency.
- Uses vanilla Valheim message paths:
  - `ShowMessage` for center-screen alerts.
  - `PlayerList` + `ChatMessage` for optional chat announcements.
- Keeps the 1.x configuration layout wherever practical.
- Per-message `Enabled` switch.

## Scheduling

Each enabled message is sent once per **real-world dedicated-server hour** at its configured `ScheduledMinuteOfHour`.

Examples:

| Value | Fires at |
| ---: | --- |
| `0` | 12:00, 1:00, 2:00, ... |
| `15` | 12:15, 1:15, 2:15, ... |
| `30` | 12:30, 1:30, 2:30, ... |
| `45` | 12:45, 1:45, 2:45, ... |

The scheduler uses the dedicated server's local system clock. A message will not fire more than once during the same hour even though the scheduler checks repeatedly during the matching minute.

## Message types

### Alert

Uses Valheim's vanilla `ShowMessage` routed RPC with `MessageHud.MessageType.Center`.

`MessageTime` controls how long the alert should stay up. On Valheim 1.0.20 the center message fades out over about four seconds and the RPC does not take a duration, so values above two seconds are kept visible by sending the same alert again every two seconds.

An alert is not sent when nobody is connected. The BepInEx log records that skip.

### Chat

Uses Valheim's vanilla `ChatMessage` RPC. On 1.0.20 a client shows that line only when both of these are true:

- The sender's platform id is in the player list while the line is displayed.
- The client's communication permission check accepts that id. A made-up platform string is rejected. A non-Steam id is accepted by the Steam client as a different-platform user.

AutoBroadcaster therefore sends the line as a non-player named `Server` with the platform id `Dedicated_Server`. That identity is added to the player-list packet, the chat line is sent, and the real player list is sent immediately afterward. The identity is taken out of the server's live player list before the send returns, so it does not increase the connected-player count or trip the server-full check.

The old 1.x `Steam_0` sender/log-suppression workaround is not used.

If the player-list anchor cannot be installed, a `Chat` message is delivered as a center alert instead and a warning is written once. `Both` still sends its own alert in that case.

### Both

Sends the same configured message through both channels independently. If one delivery path fails, the other is still attempted.

## Crossplay design

AutoBroadcaster is intended to remain **server-side only** and preserve `-crossplay` servers:

- Clients install nothing.
- The plugin does not register a mod-specific RPC.
- The plugin does not add a connection/version handshake.
- The plugin does not modify the server's crossplay setting.

`Alert` is the simplest compatibility path because it uses the same routed `ShowMessage` mechanism understood by vanilla clients. `Chat` also uses vanilla packets. After a major networking update, confirm on a Steam client and on any crossplay client that the `Server` line appears in chat. A platform that resolves the different-platform permission check asynchronously can drop the chat line; the BepInEx log still records that the server sent it.

## Configuration

Generated config location:

```text
BepInEx/config/NGT_Autobroadcaster.cfg
```

Example:

```ini
[Default Settings]
EnableBroadcasting = true
AutoBroadcastCount = 4
DebugLogging = false

[Message 01]
Enabled = true
MessageText = Welcome to the server! Be respectful and have fun.
MessageType = Alert
ScheduledMinuteOfHour = 0
MessageTime = 5

[Message 02]
Enabled = true
MessageText = Join our Discord to stay connected with the community.
MessageType = Chat
ScheduledMinuteOfHour = 15
MessageTime = 5

[Message 03]
Enabled = true
MessageText = Remember to label portals and storage so everyone can find things.
MessageType = Both
ScheduledMinuteOfHour = 30
MessageTime = 6

[Message 04]
Enabled = true
MessageText = Please report server problems to an admin.
MessageType = Alert
ScheduledMinuteOfHour = 45
MessageTime = 5
```

### Default Settings

| Key | Purpose |
| --- | --- |
| `EnableBroadcasting` | Master on/off switch. |
| `AutoBroadcastCount` | Number of `Message XX` sections to load. |
| `DebugLogging` | Enables additional scheduler/network diagnostics. |

### Message settings

| Key | Purpose |
| --- | --- |
| `Enabled` | Enables/disables one message without removing its section. |
| `MessageText` | Text sent to players. |
| `MessageType` | `Alert`, `Chat`, or `Both`. |
| `ScheduledMinuteOfHour` | Minute of each server-clock hour (`0-59`). |
| `MessageTime` | Requested alert duration in seconds. |

Saving the config while the server is running triggers a reload; a server restart is not required for normal message edits.

## Installation

1. Install BepInExPack Valheim on the **dedicated server**.
2. Build or obtain `NGT.AutoBroadcaster.dll`.
3. Place the DLL at:

```text
BepInEx/plugins/NGT.AutoBroadcaster/NGT.AutoBroadcaster.dll
```

4. Start the server once to generate the config.
5. Edit `BepInEx/config/NGT_Autobroadcaster.cfg`.

No client-side installation is required.

## Building for Valheim 1.0.20

The project targets `.NET Framework 4.8` and references the assemblies from a local Valheim Dedicated Server/BepInEx installation. Game assemblies are intentionally **not** stored in this repository.

This tree was compiled against the Valheim **1.0.20** dedicated-server assemblies (the server log reports network version **41**) and BepInExPack Valheim **5.4.2351**. Harmony (`0Harmony.dll`) is referenced from that BepInEx install. It is not a separate Thunderstore dependency.

1. Install/update the Valheim Dedicated Server to **1.0.20**.
2. Install BepInExPack Valheim **5.4.2351** on that server installation.
3. Copy `LocalPaths.props.example` to `LocalPaths.props`.
4. Set:
   - `ValheimManaged` to `<server>/valheim_server_Data/Managed`
   - `BepInExCore` to `<server>/BepInEx/core`
5. Build:

```powershell
dotnet build -c Release
```

Output:

```text
bin/Release/NGT.AutoBroadcaster.dll
```

Scheduler and config-validation tests do not need the game installed:

```powershell
dotnet test tests/NGT.AutoBroadcaster.Tests.csproj
```

`LocalPaths.props`, compiled DLLs, PDBs, build output, and packaged ZIPs are ignored by Git.

## Migrating from 1.0.1

The previous public package documented these existing keys, all of which remain recognizable:

- `[Default Settings] EnableBroadcasting`
- `[Default Settings] AutoBroadcastCount`
- `[Message XX] MessageText`
- `[Message XX] MessageType`
- `[Message XX] ScheduledMinuteOfHour`
- `[Message XX] MessageTime`

2.0.0 added, and 2.1.0 keeps:

- `[Default Settings] DebugLogging`
- `[Message XX] Enabled`

The older release also suppressed harmless `Steam_0` player-info log spam caused by its chat-sender workaround. Later versions remove that workaround rather than suppressing its log output.

No config migration is required when replacing a 2.0.0 DLL with 2.1.0. Keep the existing `NGT_Autobroadcaster.cfg`.

### Legacy screenshots

These screenshots were referenced by the 1.0.1 documentation and are retained here as historical examples of the three default broadcast entries:

- [Message 01](https://i.postimg.cc/4YLRWXwy/Message1.jpg)
- [Message 02](https://i.postimg.cc/3kSQt39G/Message2.jpg)
- [Message 03](https://i.postimg.cc/Js6CpRKJ/Message3.jpg)

## Repository layout

```text
NGT-AutoBroadcaster/
├── src/
│   ├── Plugin.cs
│   ├── MessageConfigService.cs
│   ├── MessageScheduler.cs
│   ├── BroadcastDispatcher.cs
│   ├── ServerPlayerListAnchor.cs
│   ├── MessageRules.cs
│   └── Models.cs
├── tests/
├── examples/
│   └── NGT_Autobroadcaster.cfg
├── legacy/
│   └── 1.0.1/
│       ├── README.md
│       ├── CHANGELOG.md
│       └── manifest.json
├── manifest.json
├── LocalPaths.props.example
├── NGT.AutoBroadcaster.csproj
├── CHANGELOG.md
└── README.md
```

## Troubleshooting

Watch `BepInEx/LogOutput.log` on the dedicated server. These lines are written once at startup, not on every frame:

```text
[Info   :Nerdy Gamer Tools AutoBroadcaster] Nerdy Gamer Tools AutoBroadcaster 2.1.0 loaded. Waiting for the dedicated server network to become ready.
[Info   :Nerdy Gamer Tools AutoBroadcaster] AutoBroadcaster ready on the dedicated server. 3 enabled message(s). Vanilla clients do not need this mod.
[Info   :Nerdy Gamer Tools AutoBroadcaster] Scheduled Message 01 at minute 03 (Alert).
```

When a message is due and at least one player is connected:

```text
[Info   :Nerdy Gamer Tools AutoBroadcaster] AutoBroadcaster alert (1 player(s)): Welcome to the server! Be respectful and have fun.
[Info   :Nerdy Gamer Tools AutoBroadcaster] AutoBroadcaster chat (1 player(s)): Join our Discord to be part of the community.
```

| What you see | What it means |
| --- | --- |
| The load line never appears | BepInEx did not load the DLL. Check `BepInEx/plugins/NGT.AutoBroadcaster/NGT.AutoBroadcaster.dll` and that the server is the BepInEx start script. |
| The load line appears, but the ready line does not | `ZNet` never became a dedicated server. This build does nothing on a normal client or a listen host. |
| `EnableBroadcasting is false` | The master switch is off. |
| `Skipping [Message XX]` | That section was not scheduled. The log states which value was invalid. The other messages still load. |
| `skipped (no connected players)` | The minute matched, but nobody was connected. It will try again next hour. |
| Alert works and chat does not | The client rejected the chat sender. Confirm the player-list anchor warning is absent. Crossplay clients that resolve communication permission asynchronously may drop the chat line; use `Alert` or `Both` for those players. |
| `could not find ZNet.UpdatePlayerList` | Chat falls back to a center alert. The game's player-list method changed and the anchor needs an update. |

Messages use the dedicated server's local clock. `ScheduledMinuteOfHour = 3` means xx:03 every hour, not three minutes after startup. Set `DebugLogging = true` only while diagnosing; it adds scheduler detail and is not written every tick.

Manual dedicated-server check:

1. Replace the plugin DLL and keep the existing config.
2. Start the server and confirm the ready line and one `Scheduled ...` line per enabled message.
3. Join with a vanilla client before the next configured minute.
4. At that minute, confirm one alert or one `Server` chat line, not a duplicate.
5. Leave and rejoin, or restart the server, and confirm the log has no player-list or chat exception and that the player count returns to the real number of connections.

## Project

**Nerdy Gamer Tools AutoBroadcaster**  
Website: https://nerdygamertools.com/
