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
module Mirage.Unity.ImitatePlayer

open Microsoft.FSharp.Data.UnitSystems.SI.UnitNames
open GameNetcodeStuff
open Unity.Netcode
open Mirage.Core.Getter

let get<'A> : Getter<'A> = getter "ImitatePlayer"

/// <summary>
/// A component that can attach to <b>MaskedPlayerEnemy</b> entities and imitate a specific player.
/// </summary>
type ImitatePlayer() =
    inherit NetworkBehaviour()

    let Enemy: ref<Option<MaskedPlayerEnemy>> = ref None
    let ImitatedPlayer: ref<Option<PlayerControllerB>> = ref None
    
    let getEnemy = get Enemy "Enemy"
    let getImitatedPlayer = get ImitatedPlayer "ImitatedPlayer"

    member this.Start() =
        if this.IsHost then
            Enemy.Value <- Some <| this.gameObject.GetComponent<MaskedPlayerEnemy>()