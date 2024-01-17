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
module Mirage.Patch.SpawnMirage

open FSharpPlus
open GameNetcodeStuff
open HarmonyLib
open NetworkPrefab
open UnityEngine
open Mirage.Core.Logger
open Mirage.Core.Getter

type SpawnMirage() =
    static let MaskItemPrefab = ref None
    static let getMaskItemPrefab = getter "SpawnMirage" MaskItemPrefab "MaskItemPrefab"

    [<HarmonyPostfix>]
    [<HarmonyPatch(typeof<GameNetworkManager>, "Start")>]
    static member ``save mask prefab for later use``(__instance: GameNetworkManager) =
        handleResult <| monad' {
            let! maskItemPrefab =
                findNetworkPrefab<HauntedMaskItem> __instance
                    |> Option.toResultWith "HauntedMaskItem network prefab is missing. This is likely due to a mod incompatibility."
            MaskItemPrefab.Value <- Some maskItemPrefab
        }

    [<HarmonyPostfix>]
    [<HarmonyPatch(typeof<PlayerControllerB>, "KillPlayerServerRpc")>]
    static member ``spawn mirage enemy on player death``(__instance: PlayerControllerB) =
        handleResult <| monad' {
            if __instance.IsHost then
                let! maskItemPrefab = getMaskItemPrefab "``spawn mirage enemy on player death``"
                let clone =
                    UnityEngine.Object.Instantiate<GameObject>(
                        maskItemPrefab.Prefab,
                        __instance.transform.position, 
                        Quaternion.identity
                    )
                clone.transform.localScale <- Vector3.zero
                let grabbable = clone.GetComponent<GrabbableObject>()
                grabbable.fallTime <- 1f
                grabbable.hasHitGround <- true
                grabbable.scrapPersistedThroughRounds <- false
                grabbable.isInElevator <- true
                grabbable.isInShipRoom <- true
                grabbable.NetworkObject.Spawn()
                let maskItem = clone.GetComponent<HauntedMaskItem>()
                maskItem.previousPlayerHeldBy <- __instance
                maskItem.CreateMimicServerRpc(__instance.isInsideFactory, __instance.transform.position)
        }