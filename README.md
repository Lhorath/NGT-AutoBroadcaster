# Nerdy Gamer Tools AutoBroadcaster

**Version 2.0.0** — Valheim **1.0.16** rebuild

A dedicated-server-only scheduled broadcast mod for Valheim. Vanilla clients do not need the mod installed, and the plugin does not add a custom network handshake or disable Valheim crossplay.

This repository preserves the source for the rebuilt AutoBroadcaster so future Valheim patches can be handled without losing the implementation again.

## Features

- Hourly scheduling by minute (`0-59`) for each message.
- Delivery type per message: `Alert`, `Chat`, or `Both`.
- Live config reload without restarting the dedicated server.
- Dedicated-server runtime guard; accidentally installing the DLL on a normal client does nothing.
- No custom RPC registration, network handshake, prefab, asset bundle, or client dependency.
- Uses vanilla Valheim message paths:
  - `ShowMessage` for center-screen alerts.
  - `PlayerList` + `ChatMessage` for optional chat announcements.
- Keeps the 1.x configuration layout wherever practical.
- Per-message `Enabled` switch in 2.0.0.

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

`MessageTime` controls the requested alert duration. Since the routed vanilla message does not provide a duration argument, durations above roughly two seconds are maintained by refreshing the center alert at two-second intervals, preserving the behavior expected from the older plugin.

### Chat

Uses Valheim's vanilla player-list and chat messages. Vanilla clients validate chat senders against the current player list, so AutoBroadcaster briefly adds a synthetic non-player identity named `Server`, sends the chat line, and immediately restores the real player list.

The old 1.x `Steam_0` sender/log-suppression workaround is not used by 2.0.0.

### Both

Sends the same configured message through both channels independently. If one delivery path fails, the other is still attempted.

## Crossplay design

AutoBroadcaster is intended to remain **server-side only** and preserve `-crossplay` servers:

- Clients install nothing.
- The plugin does not register a mod-specific RPC.
- The plugin does not add a connection/version handshake.
- The plugin does not modify the server's crossplay setting.

`Alert` is the simplest compatibility path because it uses the same routed `ShowMessage` mechanism understood by vanilla clients. `Chat` also uses vanilla packets, but mixed-platform chat rendering should be verified on the target server after each major Valheim networking update.

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

## Building for Valheim 1.0.16

The project targets `.NET Framework 4.8` and references the assemblies from a local Valheim Dedicated Server/BepInEx installation. Game assemblies are intentionally **not** stored in this repository.

1. Install/update the Valheim Dedicated Server to **1.0.16**.
2. Install BepInExPack Valheim **5.4.x** on that server installation.
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

`LocalPaths.props`, compiled DLLs, PDBs, build output, and packaged ZIPs are ignored by Git.

## Migrating from 1.0.1

The previous public package documented these existing keys, all of which remain recognizable in the 2.0.0 rebuild:

- `[Default Settings] EnableBroadcasting`
- `[Default Settings] AutoBroadcastCount`
- `[Message XX] MessageText`
- `[Message XX] MessageType`
- `[Message XX] ScheduledMinuteOfHour`
- `[Message XX] MessageTime`

2.0.0 adds:

- `[Default Settings] DebugLogging`
- `[Message XX] Enabled`

The older release also suppressed harmless `Steam_0` player-info log spam caused by its chat-sender workaround. The 2.0.0 source removes that workaround rather than suppressing its log output.

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
│   └── Models.cs
├── examples/
│   └── NGT_Autobroadcaster.cfg
├── manifest.json
├── LocalPaths.props.example
├── NGT.AutoBroadcaster.csproj
├── CHANGELOG.md
└── README.md
```

## Project

**Nerdy Gamer Tools AutoBroadcaster**  
Website: https://nerdygamertools.com/
