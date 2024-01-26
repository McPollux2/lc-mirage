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
module Mirage.Core.PlayerTracker

open FSharpPlus
open GameNetcodeStuff

/// <summary>
/// Keep track of players by adding/removing players to the tracker.
/// </summary>
type PlayerTracker<'Key when 'Key : comparison> =
    private
        {   players: Map<'Key, PlayerControllerB>
            toKey: PlayerControllerB -> Option<'Key>
        }

/// <summary>
/// Create a default instance of a player tracker.
/// </summary>
let defaultPlayerTracker (toKey: PlayerControllerB -> Option<'Key>) =
    {   players = zero
        toKey = toKey
    }

/// <summary>
/// Add a player to PlayerManager. If the player isn't initialized, this will simply do nothing.
/// </summary>
let addPlayer (tracker: PlayerTracker<'Key>) (player: PlayerControllerB) =
    let updatedTracker =
        tracker.toKey player |>> fun key ->
            { tracker with players = tracker.players.Add(key, player) }
    Option.defaultValue tracker updatedTracker

/// <summary>
/// Remove a player from PlayerManger. If the player isn't initialized, this will simply do nothing.
/// </summary>
let removePlayer (tracker: PlayerTracker<'Key>) (player: PlayerControllerB) =
    let updatedTracker =
        tracker.toKey player |>> fun key ->
            { tracker with players = tracker.players.Remove key }
    Option.defaultValue tracker updatedTracker

/// <summary>
/// Retrieve a player using the given arbitrary key.
/// </summary>
let getPlayer (tracker: PlayerTracker<'Key>) : 'Key -> Option<PlayerControllerB> =
    tracker.players.TryFind

/// <summary>
/// Check if the given player is tracked or not.
/// </summary>
let isPlayerTracked (tracker: PlayerTracker<'Key>) (player: PlayerControllerB) : bool =
    Option.isSome (getPlayer tracker =<< tracker.toKey player)

/// <summary>
/// Check if the given player is tracked or not (by its key).
/// </summary>
let isPlayerKeyTracked (tracker: PlayerTracker<'Key>) : 'Key -> bool =
    flip Map.containsKey tracker.players