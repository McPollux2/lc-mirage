(*
 * Copyright (C) 2024 qwbarch
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program. If not, see <https://www.gnu.org/licenses/>.
 *)
module Mirage.Unity.PlayerManager

open FSharpPlus
open GameNetcodeStuff

/// <summary>
/// A map of players, using an arbitrary key.
/// </summary>
type PlayerManager<'Key when 'Key : comparison> =
    private
        {   players: Map<'Key, PlayerControllerB>
            toKey: PlayerControllerB -> Option<'Key>
        }

/// <summary>
/// Create a default instance of a player manager.
/// </summary>
let defaultPlayerManager (toKey: PlayerControllerB -> Option<'Key>) : PlayerManager<'Key> =
    {   players = zero
        toKey = toKey
    }

/// <summary>
/// Add a player to PlayerManager. If the player isn't initialized, this will simply do nothing.
/// </summary>
let addPlayer (manager: PlayerManager<'Key>) (player: PlayerControllerB) =
    let updatedManager =
        manager.toKey player |>> fun key ->
            { manager with players = manager.players.Add(key, player) }
    Option.defaultValue manager updatedManager

/// <summary>
/// Remove a player from PlayerManger. If the player isn't initialized, this will simply do nothing.
/// </summary>
let removePlayer (manager: PlayerManager<'Key>) (player: PlayerControllerB) =
    let updatedManager =
        manager.toKey player |>> fun key ->
            { manager with players = manager.players.Remove key }
    Option.defaultValue manager updatedManager

/// <summary>
/// Retrieve a player using the given arbitrary key.
/// </summary>
let getPlayer (manager: PlayerManager<'Key>) : 'Key -> Option<PlayerControllerB> =
    manager.players.TryFind

/// <summary>
/// Check if the given player is tracked or not.
/// </summary>
let isPlayerTracked (manager: PlayerManager<'Key>) (player: PlayerControllerB) : bool =
    Option.isSome (getPlayer manager =<< manager.toKey player)