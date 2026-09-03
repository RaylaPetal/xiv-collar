## REMOVED Requirements

### Requirement: Command delivery channel
**Reason**: The websocket relay this requirement describes is being removed - it costs money/hosting to run and that cost is the reason for this change. See `collar/chat-transport` for the replacement.
**Migration**: No action for existing installs beyond re-pairing - see collar/pairing's updated "Explicit pairing handshake" requirement. Nothing to migrate on the relay side; `CollarSystem.Relay` is deleted.

### Requirement: Acknowledgement and current-state reply
**Reason**: Acknowledgements traveled over the relay connection this change removes. The chat transport has no return channel to carry them - command outcomes are observed directly (the effect happens, or it doesn't) rather than confirmed by a reply message.
**Migration**: None - this is an intentional capability reduction, not a like-for-like replacement. See design.md's Risks/Trade-offs for the reasoning.

### Requirement: Delivery only to paired, connected clients
**Reason**: "Connected to the relay" is no longer a meaningful state once there is no relay. Delivery now depends only on whether FFXIV's own server can route a `/tell` to the named character (visible via FFXIV's own system message if it can't, not something this plugin surfaces).
**Migration**: None.

### Requirement: Automatic reconnection and visible connection status
**Reason**: There is no persistent connection to reconnect or display the status of - chat delivery is stateless, per-message, and rides FFXIV's own server.
**Migration**: None.
