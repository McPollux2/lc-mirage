# Mirage

Mirage is a mod that gives masked enemies the ability to mimic a player's voice (fully synced to all players).  
This mod is required by the host and on all clients. Clients that do not have the mod will run into desynchronization issues.  
**For the optimal experience, please use push-to-talk. Using voice activity will result in a lot of empty recordings being recorded.**

## Features

- Spawn a masked enemy on death
   - Mimic the dead player's voice
   - Use the player's outfit (only vanilla for now)
   - Remove the mask off of spawned enemy
- Remove the post-round credits penalty (configurable)
- Configuration is synced to all players (only the host's config is used)

## Planned features

- [ ] Support [MoreCompany](https://thunderstore.io/c/lethal-company/p/notnotnotswipez/MoreCompany/) cosmetics
- [ ] Support [AdvancedCompany](https://thunderstore.io/c/lethal-company/p/PotatoePet/AdvancedCompany/) cosmetics
- [ ] Support [x753 MoreSuits](https://thunderstore.io/c/lethal-company/p/x753/More_Suits/)
- [ ] Make the masked enemies pick up and use items (this one's a maybe, depends on if I have time)

## Incompatible mods

While no list of incompatible mods exist yet, this will definitely fail to work with mods that spawn masked enemies.  
***You should not use any mods that change the masked enemy's spawn rate.***

## Known issues

### Why do players who disconnect no longer get their voice mimicked?

Mirage used to store all its voice clips on the host's storage, which unfortunately becomes an issue for hosts with limited available storage space.    
To address this issue, the mimicked player's voice clips are now stored on the respective player's individual storage.

When the player disconnects, they no longer broadcast their voice clips to other clients.

### Why is the mimic replaying my voice very infrequently?

This is *likely* due to having voice activity enabled. Please use push-to-talk for the optimal experience.

## Can I reupload the mod to Thunderstore?

No, reuploading the mod to Thunderstore is not permitted. If you are creating a modpack, please use the official mod.  
If you're making small changes for your friends, you will need to share the compiled ``.dll`` directly with them, and then import it locally.

## Acknowledgements

- [RugbugRedfern](https://rugbug.net) - Mirage is based off of rugbug's mod. This wouldn't exist without their ingenuity!
- [Evaisa](https://github.com/EvaisaDev) - For creating the amazing [UnityNetcodePatcher](https://github.com/EvaisaDev/UnityNetcodePatcher), which this mod uses during its build process.
