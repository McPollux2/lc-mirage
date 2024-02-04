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
open GameNetcodeStuff
open HarmonyLib
open Unity.Netcode
open Mirage.Core.Config
open Mirage.Core.Logger
open Mirage.Unity.ImitatePlayer
open Mirage.Unity.AudioStream
open Mirage.Unity.Network
open Mirage.Unity.MirageSpawner

let private init<'A when 'A : null and 'A :> EnemyAI> (networkPrefab: NetworkPrefab) =
    let enemy = networkPrefab.Prefab.GetComponent<'A>()
    if not <| isNull enemy then
        let enemyAI = enemy :> EnemyAI
        ignore <| enemyAI.gameObject.AddComponent<AudioStream>()
        ignore <| enemyAI.gameObject.AddComponent<ImitatePlayer>()

type RegisterPrefab() =
    [<HarmonyPostfix>]
    [<HarmonyPatch(typeof<GameNetworkManager>, "Start")>]
    static member ``register network prefab``(__instance: GameNetworkManager) =
        handleResult <| monad' {
            let networkManager = __instance.GetComponent<NetworkManager>()
            flip iter networkManager.NetworkConfig.Prefabs.m_Prefabs <| fun prefab ->
                if isPrefab<EnemyAI> prefab then
                    init prefab
            let! mirage =
                findNetworkPrefab<MaskedPlayerEnemy> networkManager
                    |> Option.toResultWith "MaskedPlayerEnemy network prefab is missing. This is likely due to a mod incompatibility"
            mirage.enemyType.isDaytimeEnemy <- true
            mirage.enemyType.isOutsideEnemy <- true
            //iter (ignore << mirage.gameObject.AddComponent)
            //    [   typeof<AudioStream>
            //        typeof<ImitatePlayer>
            //    ]
        }

    [<HarmonyPostfix>]
    [<HarmonyPatch(typeof<StartOfRound>, "Start")>]
    static member ``register prefab to spawn list``(__instance: StartOfRound) =
        let networkManager = UnityEngine.Object.FindObjectOfType<NetworkManager>()
        if networkManager.IsHost && not (getConfig().enableNaturalSpawn) then
            let prefabExists enemy = enemy.GetType() = typeof<MaskedPlayerEnemy>
            flip iter (__instance.levels) <| fun level ->
                flip iter (tryFind prefabExists level.Enemies) <| fun spawnable ->
                    ignore <| level.Enemies.Remove spawnable

    [<HarmonyPrefix>]
    [<HarmonyPatch(typeof<PlayerControllerB>, "Awake")>]
    static member ``register player network prefabs``(__instance: PlayerControllerB) =
        ignore <| __instance.gameObject.AddComponent<MirageSpawner>()