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
module Mirage.Patch.NetworkPrefab

open FSharpPlus
open HarmonyLib
open Unity.Netcode
open UnityEngine
open Mirage.Core.Field
open Mirage.Core.Logger
open Mirage.Unity.Enemy.ImitatePlayer
open Mirage.Unity.AudioStream
open Mirage.Unity.Network
open Mirage.Unity.Enemy.MirageSpawner

type RegisterPrefab() =
    static let MiragePrefab = ref None
    static let getMiragePrefab = getter "InitializePrefab" MiragePrefab "MiragePrefab"

    [<HarmonyPostfix>]
    [<HarmonyPatch(typeof<GameNetworkManager>, "Start")>]
    static member ``register network prefab``(__instance: GameNetworkManager) =
        handleResult <| monad' {
            let networkManager = __instance.GetComponent<NetworkManager>()
            let! mirage = 
                findNetworkPrefab<MaskedPlayerEnemy> networkManager
                    |> Option.toResultWith "MaskedPlayerEnemy network prefab is missing. This is likely due to a mod incompatibility"
            mirage.enemyType.enemyName <- "Mirage"
            mirage.enemyType.isDaytimeEnemy <- true
            mirage.enemyType.isOutsideEnemy <- true
            mirage.GetComponentsInChildren<Transform>()
                |> filter _.name.StartsWith("HeadMask")
                |> iter _.gameObject.SetActive(false)
            iter (mirage.gameObject.AddComponent >> ignore)
                [   typeof<AudioStream>
                    typeof<ImitatePlayer>
                ]
            set MiragePrefab mirage
            let maskedPrefabs = findNetworkPrefabs<HauntedMaskItem> networkManager
            flip iter maskedPrefabs <| fun mask ->
                ignore <| mask.gameObject.AddComponent<MirageSpawner>()
        }

    [<HarmonyPostfix>]
    [<HarmonyPatch(typeof<StartOfRound>, "Start")>]
    static member ``register prefab to spawn list``(__instance: StartOfRound) =
        handleResult <| monad' {
            let networkManager = UnityEngine.Object.FindObjectOfType<NetworkManager>()
            if networkManager.IsHost then
                let! miragePrefab = getMiragePrefab "``register prefab to spawn list``"
                let prefabExists enemy = enemy.GetType() = typeof<MaskedPlayerEnemy>
                let registerPrefab (level: SelectableLevel) =
                    let spawnable = new SpawnableEnemyWithRarity()
                    spawnable.enemyType <- miragePrefab.enemyType
                    spawnable.rarity <- 0
                    level.Enemies.Add spawnable
                flip iter (__instance.levels) <| fun level ->
                    if not <| exists prefabExists level.Enemies then
                        registerPrefab level
        }