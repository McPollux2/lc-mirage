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
module Mirage.Unity.Imitation.Component

open System.Threading.Tasks
open GameNetcodeStuff
open Unity.Netcode
open Cysharp.Threading.Tasks
open Mirage.Core.Getter
open Mirage.Core.Logger

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

    let rec runImitationLoop () =
        task {
            do! Task.Delay 1000
            logInfo "Imitating player"
            return! runImitationLoop();
        }

    member this.Start() =
        if this.IsHost then
            Enemy.Value <- Some <| this.gameObject.GetComponent<MaskedPlayerEnemy>()
            runImitationLoop().AsUniTask().Forget()