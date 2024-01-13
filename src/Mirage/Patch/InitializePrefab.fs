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
module Mirage.Patch.InitializePrefab

open Cysharp.Threading.Tasks
open FSharpPlus
open HarmonyLib
open Unity.Netcode
open UnityEngine
open Mirage.Core.Logger
open Mirage.Unity.Audio.AudioStream
open System.Threading.Tasks

type InitializePrefab() =
    static let mutable miragePrefab = None
    static let getMiragePrefab () = Option.toResultWith "Mirage prefab has not been initialized yet." miragePrefab

    // TODO: Remove this.
    [<HarmonyPrefix>]
    [<HarmonyPatch(typeof<Debug>, "Log", [| typeof<obj> |])>]
    static member RemoveAnnoyingLogs (message: obj) =
        if message :? string then
            let m = message :?> string
            message :? string &&
            not (m.StartsWith("Looking at fo") || m.StartsWith("Look rotation viewing") || m.StartsWith("STARTING AI") || m.StartsWith("Setting zap mode"))
        else
            true

    [<HarmonyPostfix>]
    [<HarmonyPatch(typeof<GameNetworkManager>, "Start")>]
    static member ``register network prefab``(__instance: GameNetworkManager) =
        handleError <| monad' {
            logInfo "Initializing network prefab."
            let networkPrefabs = __instance.GetComponent<NetworkManager>().NetworkConfig.Prefabs.m_Prefabs
            let isMaskedPrefab (networkPrefab: NetworkPrefab) =
                not << isNull <| networkPrefab.Prefab.GetComponent<MaskedPlayerEnemy>()
            let errorMessage = "MaskedPlayerEnemy network prefab could not be found. This is probably due to mod incompatibility."
            let! networkPrefab =
                List.ofSeq networkPrefabs
                    |> List.tryFind isMaskedPrefab
                    |> Option.toResultWith errorMessage
            let prefab = networkPrefab.Prefab.GetComponent<MaskedPlayerEnemy>()
            prefab.enemyType.enemyName <- "Mirage"
            prefab.enemyType.isDaytimeEnemy <- true
            prefab.enemyType.isOutsideEnemy <- true
            ignore <| prefab.gameObject.AddComponent<AudioStream>()
            miragePrefab <- Some prefab
            logInfo "Finished initializing network prefab."
        }

    [<HarmonyPostfix>]
    [<HarmonyPatch(typeof<StartOfRound>, "Start")>]
    static member ``register prefab to spawn list``(__instance: StartOfRound) =
        handleError <| monad' {
            let networkManager = UnityEngine.Object.FindObjectOfType<NetworkManager>()
            if networkManager.IsHost then
                logInfo "Registering prefab to spawn list."
                let! prefab = getMiragePrefab()
                let prefabExists enemy = enemy.GetType() = typeof<MaskedPlayerEnemy>
                let registerPrefab (level: SelectableLevel) =
                    let spawnable = new SpawnableEnemyWithRarity()
                    spawnable.enemyType <- prefab.enemyType
                    spawnable.rarity <- 100 // TODO: Make this reasonable.
                    level.Enemies.Add spawnable
                flip iter (__instance.levels) <| fun level ->
                    if not <| prefabExists level.Enemies then
                        registerPrefab level
                logInfo "Finished registering prefab to spawn list."
            
                // TODO: Remove everything below this.
                let roundManager = UnityEngine.Object.FindObjectOfType<RoundManager>()
                let rec keepSendingAudio () : Task<Unit> =
                    task {
                        while not __instance.allPlayersDead do
                            let enemyFilter (enemy: EnemyAI) : Option<MaskedPlayerEnemy> =
                                match enemy with
                                    | :? MaskedPlayerEnemy as maskedEnemy -> Some maskedEnemy
                                    | _ -> None
                            let mirageEnemies = 
                                List.ofSeq roundManager.SpawnedEnemies
                                    |> choose enemyFilter
                            let playAudio (enemy: MaskedPlayerEnemy) =
                                task {
                                    let audioStream = enemy.GetComponent<AudioStream>()
                                    if not <| audioStream.IsServerRunning() then
                                        logInfo $"Found {mirageEnemies.Length} mirage enemies spawned."
                                        logInfo $"Starting new audio."
                                        logInfo "Streaming audio"
                                        audioStream.StreamAudioFromFile $"{Application.dataPath}/../BepInEx/plugins/asset/ram-ranch.mp3"
                                        do! Task.Delay 200
                                }
                            let! _ = traverse playAudio mirageEnemies
                            //logInfo $"found enemies: {mirageEnemies.Length}"
                            return! Task.Delay(1000)
                    }
                keepSendingAudio().AsUniTask().Forget()
        }