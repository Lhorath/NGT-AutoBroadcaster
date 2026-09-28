# Nerdy Gamer Tools AutoBroadcaster

Version: `1.0.1`

Server-side scheduled broadcast mod for Valheim  
Vanilla clients are fully compatible (no client mod required).

## Features

- Hourly schedule by minute (`0-59`) per message entry.
- Delivery type per message: `Alert`, `Chat`, or `Both`.
- Hot-reload config changes without server restart.
- Dedicated-server-safe chat/alert broadcast paths.
- Suppresses known harmless `Steam_0` player-info log spam.

## Config

Location: `YOURSERVERDIR/BepInEx/config/NGT_Autobroadcaster.cfg`

Sections:

- `[Default Settings]`
  - `EnableBroadcasting`
  - `AutoBroadcastCount`
- `[Message 01..NN]`
  - `MessageText`
  - `MessageType`
  - `ScheduledMinuteOfHour`
  - `MessageTime`

## Installation

1. Install BepInEx on server.
2. Place `NGT.AutoBroadcaster.dll` in `BepInEx/plugins/`.
3. Start server once to generate config.
4. Edit `NGT_Autobroadcaster.cfg` and save.

## Default Message Examples

### Message 01

![Message 01 Screenshot](https://i.postimg.cc/4YLRWXwy/Message1.jpg)

### Message 02

![Message 02 Screenshot](https://i.postimg.cc/3kSQt39G/Message2.jpg)

### Message 03

![Message 03 Screenshot](https://i.postimg.cc/Js6CpRKJ/Message3.jpg)
