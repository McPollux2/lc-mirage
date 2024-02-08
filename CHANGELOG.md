## Changelog

### 1.0.8

- Fixed a bug where voice filters weren't being applied, causing mimicked voices to behave unpredictably
- Fixed a bug where **SpawnOnPlayerDeath** could spawn two masked enemies in certain scenarios

### 1.0.7

- Hotfix: If the local player's mimicking voice is muted, it now becomes unmuted when the player is dead

### 1.0.6

Thanks to [jarylc](https://github.com/jarylc) for the initial [fixes](https://gist.github.com/jarylc/3f6fc305466d4970deae16629469b3c2) for masked enemy spawns.

- Rewrite masked enemy spawn and mimicking logic
    - This is less invasive now and should conflict with other mods less often
    - Fixed a bug where naturally spawned masked enemies weren't synced properly
- Fixed a bug where players not haunted by the ghost girl could hear the mimicking voice.
- Add configuration option to only spawn player on death, when the dying player is alone
- Add configuration option to mute the mimicking voice when mimicking the local player
- Add configuration option to adjust voice mimicking delay for non-masked enemies
- Add configuration option to mimic voices on modded enemies

### 1.0.5

- Use audio filters to sound more like vanilla voices
- Mimicked voices on the outside can no longer be heard from inside the facility (and vice-versa)
- Masked enemies no longer mimic voices while hiding on the ship

### 1.0.4

- Support voice mimicking on all vanilla enemies.
- Add configuration option for mask texture and arms-out animation (for masked enemies).
- Dead enemies no longer mimic voices.

### 1.0.3

- Support voice activity.

### 1.0.2

- Bundled all dependencies I control into a single core lib (users now pull less packages).

### 1.0.1

- Spawn on player death is now configurable.
- Naturally spawned masked enemies now mimic a player at random.
- Dependencies are now separated into its own packages.

### 1.0.0

- Initial release.
