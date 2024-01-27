# Mirage

Mirage is a mod that gives masked enemies the ability to mimic a player's voice (fully synced to all players).  
This mod is required by the host and on all clients. Clients that do not have the mod will run into desynchronization issues.

**Note: Only push-to-talk is supported. Using voice activity will result in no voice playback.**

## Features

- Spawn a masked enemy on death (like a player turning into a zombie)
   - Mimic the dead player's voice to all nearby players, as well as spectators
   - Use the player's outfit (this is vanilla behaviour)
   - Remove the mask off of spawned enemy
- Remove the post-round credits penalty (configurable)
- Vanilla masked enemy spawn is disabled (configurable, this is disabled by default because a mirage only mimics whoever dies)
- Configuration is synced to all players (only the host's config is used)

## Planned features

### Support voice activity

While voice activity was initially intended for the initial release, its current implementation results in a lot of undesirable audio clips,
causing voice playback to rarely mimic the clips that you would normally expect.

This will be implemented after I figure out how to filter the undesirable audio clips, but is currently a low priority for me.

## Incompatible mods

While no list of incompatible mods exist yet, this will definitely fail to work with mods that spawn masked enemies.  
***You should not use any mods that change the masked enemy's spawn rate.***

## Known issues

### Why do players who disconnect no longer get their voice mimicked?

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
