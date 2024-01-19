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
open Microsoft.FSharp.Core.Option

/// <summary>
/// Keeps track of players, until a player is marked as disabled.
/// </summary>
type PlayerManager<'PlayerId when 'PlayerId : comparison> =
    private { players: Map<'PlayerId, PlayerControllerB> }

/// <summary>
/// Initialize the player manager. This should be called at the start of
/// a round, when all player scripts are ready (have been added to the list).
/// </summary>
let defaultPlayerManager<'PlayerId when 'PlayerId : comparison> (startOfRound: StartOfRound) (toPlayerId: PlayerControllerB -> 'PlayerId) =
    {   // Since non-connected players have a client id of 0,
        // reversing the list before converting it to Map will keep only the
        // first player (always the host).
        players =
            startOfRound.allPlayerScripts
                |> Array.map (fun player -> (toPlayerId player, player))
                |> List.ofSeq
                |> List.rev
                |> Map.ofList
    }

/// <summary>
/// Mark the player as disabled. The given player is no longer tracked.<br />
/// If an invalid player id is given, this will simply do nothing.
/// </summary>
let disablePlayer<'PlayerId when 'PlayerId : comparison> (manager: PlayerManager<'PlayerId>) (playerId: 'PlayerId) =
    { manager with
        players = Map.remove playerId <| manager.players
    }

/// <summary>
/// Retrieve the player if it hasn't been disabled by <b>disablePlayer</b>.
/// </summary>
let getActivePlayer<'PlayerId when 'PlayerId : comparison> (manager: PlayerManager<'PlayerId>) : 'PlayerId -> Option<PlayerControllerB> =
    manager.players.TryFind

/// <summary>
/// Whether the given player is still active or not.
/// </summary>
let isPlayerActive<'PlayerId when 'PlayerId : comparison> (manager: PlayerManager<'PlayerId>) : 'PlayerId -> bool =
    isSome << getActivePlayer manager