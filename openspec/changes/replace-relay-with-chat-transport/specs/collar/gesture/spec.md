## REMOVED Requirements

### Requirement: Catalog shared with paired Owner
**Reason**: This requirement described relaying the Sub's scanned gesture catalog to the Owner's client over the websocket relay being removed in this change. There is no live channel left to push it over.
**Migration**: The Sub still scans locally (unchanged - see "Automatic gesture catalog from installed mods") to decide what to name each alias. Sharing those alias names with the Owner is now a manual/negotiated step, the same as sharing a character name during pairing - not something the plugin automates.
