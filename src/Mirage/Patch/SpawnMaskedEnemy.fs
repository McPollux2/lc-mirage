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
module Mirage.Patch.SpawnMaskedEnemy

open System.Collections.Generic
open FSharpPlus
open HarmonyLib
open GameNetcodeStuff
open Unity.Netcode
open UnityEngine
open Mirage.Core.Config
open Mirage.Core.Field
open Mirage.Core.Logger
open Mirage.Unity.Network
open Mirage.Unity.MimicPlayer

let private get<'A> = getter<'A> "SpawnMaskedEnemy"

type SpawnMaskedEnemy() =
    static let random = new System.Random()
    static let killedPlayers = new HashSet<uint64>()

    static let MaskItem = field<HauntedMaskItem>()
    static let getMaskItem = get MaskItem "MaskItem"

    static let spawnMaskedEnemy (player: PlayerControllerB) =
        handleResult <| monad' {
            let methodName = "spawnMaskedEnemy"
            let! maskItem = getMaskItem methodName
            let rotationY = player.transform.eulerAngles.y
            let maskedEnemy =
                Object.Instantiate<GameObject>(
                    maskItem.mimicEnemy.enemyPrefab,
                    player.transform.position,
                    Quaternion.Euler <| Vector3(0f, rotationY, 0f)
                )
            maskedEnemy.GetComponent<MaskedPlayerEnemy>().mimickingPlayer <- player
            maskedEnemy.GetComponentInChildren<NetworkObject>().Spawn(destroyWithScene = true)
        }

    [<HarmonyPostfix>]
    [<HarmonyPatch(typeof<GameNetworkManager>, "Start")>]
    static member ``save mask prefab for later use``(__instance: GameNetworkManager) =
        handleResult <| monad' {
            let! maskItem =
                findNetworkPrefab<HauntedMaskItem> (__instance.GetComponent<NetworkManager>())
                    |> Option.toResultWith "HauntedMaskItem network prefab is missing. This is likely due to a mod incompatibility."
            set MaskItem maskItem
        }

    [<HarmonyPostfix>]
    [<HarmonyPatch(typeof<StartOfRound>, "StartGame")>]
    static member ``reset killed players``() = killedPlayers.Clear()

    [<HarmonyPostfix>]
    [<HarmonyPatch(typeof<PlayerControllerB>, "KillPlayerServerRpc")>]
    static member ``spawn a masked enemy on player death (if configuration is enabled)``(
        __instance: PlayerControllerB,
        causeOfDeath: int,
        deathAnimation: int,
        spawnBody: bool,
        bodyVelocity: Vector3
    ) =
        if __instance.IsHost && killedPlayers.Add __instance.playerClientId then
            let playerKilledByMaskItem = 
                causeOfDeath = int CauseOfDeath.Suffocation
                    && spawnBody
                    && bodyVelocity.Equals Vector3.zero
            let playerKilledByMaskedEnemy =
                causeOfDeath = int CauseOfDeath.Strangulation
                    && deathAnimation = 4
            let config = getConfig()
            let isPlayerAloneRequired = not config.spawnOnlyWhenPlayerAlone || __instance.isPlayerAlone
            let spawnRateSuccess () = random.Next(1, 101) <= config.spawnOnPlayerDeath
            if not playerKilledByMaskItem
                && not playerKilledByMaskedEnemy
                && spawnBody
                && isPlayerAloneRequired
                && spawnRateSuccess()
            then
                spawnMaskedEnemy __instance

    [<HarmonyPrefix>]
    [<HarmonyPatch(typeof<MaskedPlayerEnemy>)>]
    [<HarmonyPatch("SetHandsOutServerRpc")>]
    [<HarmonyPatch("SetHandsOutClientRpc")>]
    static member ``disable mirage hands out``() = getConfig().enableArmsOut


    [<HarmonyPrefix>]
    [<HarmonyPatch(typeof<MaskedPlayerEnemy>, "Start")>]
    static member ``start mimicking player``(__instance: MaskedPlayerEnemy) =
        __instance.GetComponent<MimicPlayer>().StartMimicking()

    [<HarmonyPostfix>]
    [<HarmonyPatch(typeof<MaskedPlayerEnemy>, "Start")>]
    static member ``remove mask texture``(__instance: MaskedPlayerEnemy) =
        if not <| getConfig().enableMask then
            __instance.GetComponentsInChildren<Transform>()
                |> filter _.name.StartsWith("HeadMask")
                |> iter _.gameObject.SetActive(false)