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

## Discord

If you have questions, and/or want to stay up-to-date with the mod:

1. Join the LC [modding discord](https://discord.gg/lcmod)
2. Go to the Mirage [release thread](https://discord.com/channels/1168655651455639582/1200695291972685926) and ask your question

## What configuration options should I use for a more vanilla experience?

- Set ``EnablePenalty`` to ``true`` to allow post-round credit penalties to apply.
- Set ``EnableNaturalSpawn`` to ``true`` to allow masked enemies to naturally spawn (recommended to use a spawn control mod on top of this).
- Set ``SpawnOnPlayerDeath`` to ``0`` to disable the zombie-like spawning mechanic.
- **Recommended:** Set ``MuteLocalPlayerVoice`` to ``true`` to make a voice mimic muted for you locally while it still plays for others (unless you die).

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

To stay up to date with the latest changes, click [here](https://thunderstore.io/c/lethal-company/p/qwbarch/Mirage/changelog) to view the changelog.
