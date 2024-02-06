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

open FSharpPlus
open Unity.Netcode
open System.Threading
open GameNetcodeStuff
open Mirage.Core.Config
open Mirage.Core.Field
open Mirage.Core.Logger
open Mirage.Core.Monad
open Mirage.Core.Audio.Recording
open Mirage.Unity.AudioStream
open Mirage.Unity.MirageSpawner
open UnityEngine

/// <summary>
/// A component that can attach to <b>MaskedPlayerEnemy</b> entities and imitate a specific player.
/// </summary>
type ImitatePlayer() =
    inherit NetworkBehaviour()

    let random = new System.Random()
    let canceller = new CancellationTokenSource()
    let mutable checkInterval = Random.Range(0f, 0.4f)
    let mutable occluded = false

    // Bandaid fix to prevent imitation from running twice if it's already been started
    // in .Start(), and has been requested to run in the client rpc.
    // TODO: Make this more proper, this bandaid fix definitely isn't required.
    let mutable started = false

    let Enemy = field<EnemyAI>()
    let AudioStream = field<AudioStream>()
    let LowPassFilter = field<AudioLowPassFilter>()
    let ReverbFilter = field<AudioReverbFilter>()

    let imitatePlayer (this: ImitatePlayer) (player: PlayerControllerB) =
        ignore <| monad' {
            let! audioStream = getValue AudioStream
            flip iter (getRandomRecording random) <| fun recording ->
                try
                    if this.IsHost then
                        audioStream.StreamAudioFromFile recording
                    else
                        audioStream.UploadAndStreamAudioFromFile(player.actualClientId, recording)
                with | error ->
                    logError $"Failed to imitate player: {error}"
        }

    let rec runImitationLoop this (enemy: EnemyAI) player =
        async {
            try
                imitatePlayer this player
            with | error ->
                logError $"Failed to imitate player: {error}"
            let config = getConfig()
            let delay = random.Next(config.imitateMinDelay, config.imitateMaxDelay + 1)
            return! liftAsync <| Async.Sleep delay
            if not enemy.isEnemyDead then
                return! runImitationLoop this enemy player
        }
    
    let startImitation this enemy player = 
        if not started then
            started <- true
            toUniTask_ canceller.Token <| runImitationLoop this enemy player

    let isOccluded (this: ImitatePlayer) =
        StartOfRound.Instance <> null
            && Physics.Linecast(this.transform.position, StartOfRound.Instance.audioListener.transform.position, 256, QueryTriggerInteraction.Ignore)

    let imitateVoiceIfLocalPlayer this (mirage: MaskedPlayerEnemy) config =
        if config.enableMaskedEnemy && mirage.mimickingPlayer.actualClientId = GameNetworkManager.Instance.localPlayerController.actualClientId then
            startImitation this mirage mirage.mimickingPlayer

    member this.Awake() =
        let lowPassFilter = this.gameObject.AddComponent<AudioLowPassFilter>()
        lowPassFilter.cutoffFrequency <- 20000f
        set LowPassFilter lowPassFilter
        let reverbFilter = this.gameObject.AddComponent<AudioReverbFilter>()
        reverbFilter.reverbPreset <- AudioReverbPreset.User
        reverbFilter.dryLevel <- -1f
        reverbFilter.decayTime <- 0.8f
        reverbFilter.room <- -2300f
        set ReverbFilter reverbFilter

    member this.Start() =
        let audioStream = this.gameObject.GetComponent<AudioStream>()
        set AudioStream audioStream
        let audioSource = audioStream.GetAudioSource()
        audioSource.spatialBlend <- 1f
        occluded <- isOccluded this

        let config = getConfig()
        let enemy = this.gameObject.GetComponent<EnemyAI>()
        set Enemy enemy
        if this.IsHost && enemy :? MaskedPlayerEnemy then
            let mirage = enemy :?> MaskedPlayerEnemy
            if isNull mirage.mimickingPlayer then
                let round = StartOfRound.Instance
                let players = round.allPlayerScripts
                let playerId = random.Next <| round.connectedPlayersAmount + 1
                mimicPlayer mirage players[playerId]
                this.SetMimickingPlayerClientRpc playerId
            imitateVoiceIfLocalPlayer this mirage config
        else
            let random = new System.Random()
            let round = StartOfRound.Instance
            let imitate enabled =
                if enabled then
                    let player = round.allPlayerScripts[random.Next <| round.connectedPlayersAmount + 1]
                    // Host has an id of 0.
                    if this.IsHost then
                        if player.playerClientId = 0UL then
                            startImitation this enemy player
                        else
                            let sendParams = new ClientRpcSendParams(TargetClientIds = [|player.actualClientId|])
                            let clientParams = new ClientRpcParams(Send = sendParams)
                            this.ImitatePlayerClientRpc clientParams
            match enemy with
                | :? BaboonBirdAI -> imitate config.enableBaboonHawk
                | :? FlowermanAI -> imitate config.enableBracken
                | :? SandSpiderAI -> imitate config.enableSpider
                | :? DocileLocustBeesAI -> imitate config.enableBees
                | :? SpringManAI -> imitate config.enableCoilHead
                | :? SandWormAI -> imitate config.enableEarthLeviathan
                | :? MouthDogAI -> imitate config.enableEyelessDog
                | :? ForestGiantAI -> imitate config.enableForestKeeper
                | :? DressGirlAI -> imitate config.enableGhostGirl
                | :? HoarderBugAI -> imitate config.enableHoardingBug
                | :? BlobAI -> imitate config.enableHygrodere
                | :? JesterAI -> imitate config.enableJester
                | :? DoublewingAI -> imitate config.enableManticoil
                | :? NutcrackerEnemyAI -> imitate config.enableNutcracker
                | :? CentipedeAI -> imitate config.enableSnareFlea
                | :? PufferAI -> imitate config.enableSporeLizard
                | :? CrawlerAI -> imitate config.enableThumper
                | _ -> () // Unsupported modded monster.

    override _.OnDestroy() =
        try canceller.Cancel()
        with | _ -> ()
        dispose canceller
    
    // Only for non-masked enemies. TODO: Redo this source code to be more proper.
    [<ClientRpc>]
    member this.ImitatePlayerClientRpc(_: ClientRpcParams) =
        let enemy = this.GetComponent<EnemyAI>()
        startImitation this enemy StartOfRound.Instance.localPlayerController

    // Only for masked enemies. TODO: Redo this source code to be more proper.
    [<ClientRpc>]
    member this.SetMimickingPlayerClientRpc(playerId) =
        let mirage = this.GetComponent<MaskedPlayerEnemy>()
        let player = StartOfRound.Instance.allPlayerScripts[playerId]
        mimicPlayer mirage player
        imitateVoiceIfLocalPlayer this mirage <| getConfig()

    member this.Update() =
        // TODO: Use result instead to get proper error messages.
        ignore <| monad' {
            let! audioStream = getValue AudioStream
            let audioSource = audioStream.GetAudioSource()
            let! lowPassFilter = getValue LowPassFilter
            let! reverbFilter = getValue ReverbFilter
            let! enemy = getValue Enemy
            let round = StartOfRound.Instance
            let localPlayer = round.localPlayerController
            
            if enemy.isEnemyDead || enemy :? MaskedPlayerEnemy && (enemy :?> MaskedPlayerEnemy).crouching then
                audioSource.mute <- true
            else if enemy.isOutside then
                reverbFilter.enabled <- false
                audioSource.mute <- localPlayer.isInsideFactory
            else
                audioSource.mute <- not localPlayer.isInsideFactory
                let listenerPosition = round.audioListener.transform.position
                let distanceToListener = Vector3.Distance(listenerPosition, this.transform.position)
                let normalizedDistanceReverb = 0f - 3.4f * distanceToListener / audioSource.maxDistance / 5f
                let clampedDryLevel = Mathf.Clamp(normalizedDistanceReverb, -300f, -1f)
                let lerpFactorReverb = Time.deltaTime * 8f
                reverbFilter.dryLevel <-
                    Mathf.Lerp(
                        reverbFilter.dryLevel,
                        clampedDryLevel,
                        lerpFactorReverb
                    )
                reverbFilter.enabled <- true

            if occluded then
                let distance = Vector3.Distance(StartOfRound.Instance.audioListener.transform.position, this.transform.position)
                let normalizedDistance = 2500f / distance / audioSource.maxDistance / 2f
                let clampedFrequency = Mathf.Clamp(normalizedDistance, 900f, 4000f)
                let lerpFactor = Time.deltaTime * 8f
                lowPassFilter.cutoffFrequency <- Mathf.Lerp(lowPassFilter.cutoffFrequency, clampedFrequency, lerpFactor)
            else
                lowPassFilter.cutoffFrequency <- Mathf.Lerp(lowPassFilter.cutoffFrequency, 10000f, Time.deltaTime * 8f);
            
            if checkInterval >= 0.5f then
                checkInterval <- 0f
                occluded <- isOccluded this
            else
                checkInterval <- checkInterval + Time.deltaTime
        }