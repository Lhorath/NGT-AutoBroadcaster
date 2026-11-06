# Changelog

All notable changes to **Nerdy Gamer Tools AutoBroadcaster** are documented here.

The project follows semantic versioning.

## [2.1.0] - 2026-10-19

### Fixed

- Broadcasting no longer depends on `-batchmode`. The plugin now waits for `ZNet.IsServer()` and `ZNet.IsDedicated()`, which is true on the dedicated-server build and false on a normal client or listen host.
- Chat delivery on Valheim 1.0.20. Clients discard `ChatMessage` unless the sender is in the current player list, and a synthetic id that is not a real platform user is rejected by the client permission check. Chat now sends from a temporary `Dedicated_Server` identity that is included in the player list for that send and removed before the connected-player count is used again.
- A scheduled alert or chat line with nobody connected is logged once and skipped, instead of logging a successful send that no player received.
- The one-time chat failure warning no longer hides later delivery errors.

### Changed

- Compatibility target is Valheim **1.0.20** (network version 41) with BepInExPack Valheim **5.4.2351**.
- Center alerts still use vanilla `ShowMessage`. On 1.0.20 that message fades over about four seconds; `MessageTime` values above two seconds are still extended by resending it.
- Startup logs when the dedicated server is actually ready, including each enabled message and its minute.

### Compatibility

- Existing `NGT_Autobroadcaster.cfg` keys are unchanged. No config migration is required.

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
