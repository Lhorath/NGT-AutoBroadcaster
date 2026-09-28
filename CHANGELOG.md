# Changelog

All notable changes to **Nerdy Gamer Tools AutoBroadcaster** are documented here.

The project follows semantic versioning.

## [2.0.0] - 2026-09-28

### Rebuilt

- Rebuilt the plugin source for Valheim 1.0.16.
- Preserved the original minute-of-hour scheduling model.
- Preserved the legacy configuration keys wherever practical.
- Kept the plugin dedicated-server only with no client installation requirement.
- Kept the implementation on vanilla Valheim message/network paths so the mod does not add its own connection handshake or disable crossplay.

### Added

- Per-message `Enabled` switch.
- `DebugLogging` configuration switch.
- Live config reload handled on the Unity main thread.
- Repository build documentation and `LocalPaths.props.example`.

### Changed

- Reworked chat delivery to use a temporary vanilla-compatible player-list identity named `Server` before sending `ChatMessage`, then immediately restore the real player list.
- Alert and chat failures are isolated so one channel does not prevent the other from being attempted when `MessageType = Both`.

### Removed

- Removed the old `Steam_0` chat-sender workaround.
- Removed the Harmony/ZLog patch used only to suppress the resulting harmless player-info log spam.

## [1.0.1] - 2026-04-13

### Added

- Initial public release of `Nerdy Gamer Tools Autobroadcaster`.
- Scheduled hourly broadcasts using minute-of-hour triggers (`0-59`).
- Broadcast modes: `ALERT`, `CHAT`, and `BOTH`.
- Dynamic message slot discovery from config (`Message1+`, `Message2+`, etc.).
- Live config reload when `NGT_Autobroadcaster.cfg` changes.
- Example screenshots in the release documentation.

### Legacy package metadata

- Package version: `1.0.1`.
- BepInEx dependency: `denikson-BepInExPack_Valheim-5.4.2202`.
- Server-side package description stated that vanilla clients required no client mod.
