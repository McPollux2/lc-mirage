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
open Mirage.Core.Logger
open Mirage.Unity.Audio.AudioStream
open Mirage.Unity.ImitatePlayer
open Mirage.Core.Getter

/// <summary>
/// Find the network prefab, specified by the generic parameter.
/// </summary>
let findNetworkPrefab<'A> (manager: GameNetworkManager) : Option<NetworkPrefab> =
    let networkManager = manager.GetComponent<NetworkManager>()
    let networkPrefabs = networkManager.NetworkConfig.Prefabs.m_Prefabs
    let isTargetPrefab (networkPrefab: NetworkPrefab) =
        not << isNull <| networkPrefab.Prefab.GetComponent typeof<'A>
    List.tryFind isTargetPrefab <| List.ofSeq networkPrefabs

type RegisterPrefab() =
    static let MiragePrefab = ref None
    static let getMiragePrefab = getter "InitializePrefab" MiragePrefab "MiragePrefab"

    // TODO: Remove this.
    [<HarmonyPrefix>]
    [<HarmonyPatch(typeof<Debug>, "Log", [| typeof<obj> |])>]
    static member RemoveAnnoyingLogs(message: obj) =
        if message :? string then
            let m = message :?> string
            not (
                m.StartsWith("Looking at fo") || m.StartsWith("Look rotation viewing") || m.StartsWith("STARTING AI") || m.StartsWith("Setting zap mode")
            )
        else
            true

    [<HarmonyPostfix>]
    [<HarmonyPatch(typeof<GameNetworkManager>, "Start")>]
    static member ``register network prefab``(__instance: GameNetworkManager) =
        handleResult <| monad' {
            let! networkPrefab = 
                findNetworkPrefab<MaskedPlayerEnemy> __instance
                    |> Option.toResultWith "MaskedPlayerEnemy network prefab is missing. This is likely due to a mod incompatibility"
            let prefab = networkPrefab.Prefab.GetComponent<MaskedPlayerEnemy>()
            prefab.enemyType.enemyName <- "Mirage"
            prefab.enemyType.isDaytimeEnemy <- true
            prefab.enemyType.isOutsideEnemy <- true
            iter (prefab.gameObject.AddComponent >> ignore)
                [   typeof<AudioStream>
                    typeof<ImitatePlayer>
                ]
            MiragePrefab.Value <- Some prefab
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