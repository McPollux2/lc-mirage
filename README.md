# Mirage

Mirage is a mod that gives masked enemies the ability to mimic a player's voice (fully synced to all players).  
**This mod is required by the host and on all clients.** Clients that do not have the mod will run into desynchronization issues.

## Features

- Spawn a masked enemy on player death (like a player turning into a zombie, chance is configurable)
   - Mimic the dead player's voice to all nearby players, as well as spectators
   - Use the player's outfit (this is vanilla behaviour)
   - Remove the mask off of masked enemy
   - Remove the arms out animation off of masked enemy
- Naturally spawned masked enemies mimic a random player (with the features mentioned above)
- Remove the post-round credits penalty (configurable)
- Configuration is synced to all players (only the host's config is used)

## Recommended mods

[DissonanceLagFix](https://thunderstore.io/c/lethal-company/p/linkoid/DissonanceLagFix/) - This plugin significantly reduces the duration of lag spikes simply by changing the log level of DissonanceVoip.

## Why do players who disconnect no longer get their voice mimicked?

Voices of each player are stored on the respective player's individual storage. Since
the player is no longer connected, their client cannot send audio clips to other clients.

##  I have a suggestion for the mod, and/or have found a bug

Whether you have a suggestion or have a bug to report, please submit it as an issue [here](https://github.com/qwbarch/lc-mirage/issues/new).

## Can I reupload the mod to Thunderstore?

No, reuploading the mod to Thunderstore is not permitted. If you are creating a modpack, please use the official mod.  
If you're making small changes for your friends, you will need to share the compiled ``.dll`` directly with them, and then import it locally.

## Acknowledgements

- [RugbugRedfern](https://rugbug.net) - Mirage is based off of rugbug's mod. This wouldn't exist without their ingenuity!
- [Evaisa](https://github.com/EvaisaDev) - For creating the amazing [UnityNetcodePatcher](https://github.com/EvaisaDev/UnityNetcodePatcher), which this mod uses during its build process.

## Changelog

Click [here](https://thunderstore.io/c/lethal-company/p/qwbarch/Mirage/changelog) to view the changelog.