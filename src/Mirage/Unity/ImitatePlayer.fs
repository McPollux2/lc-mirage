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

#nowarn "40"

open System
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

/// <summary>
/// A component that can attach to <b>MaskedPlayerEnemy</b> entities and imitate a specific player.
/// </summary>
type ImitatePlayer() =
    inherit NetworkBehaviour()

    let random = new Random()
    let canceller = new CancellationTokenSource()

    let AudioStream = field<AudioStream>()

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
        toUniTask_ canceller.Token <| runImitationLoop this enemy player

    member this.Start() =
        let audioStream = this.gameObject.GetComponent<AudioStream>()
        set AudioStream audioStream
        let audioSource = audioStream.GetAudioSource()
        audioSource.spatialBlend <- 1f

        let config = getConfig()
        let enemy = this.gameObject.GetComponent<EnemyAI>()
        if this.IsHost && enemy :? MaskedPlayerEnemy then
            let mirage = enemy :?> MaskedPlayerEnemy
            if config.enableMaskedEnemy && mirage.mimickingPlayer.actualClientId = GameNetworkManager.Instance.localPlayerController.actualClientId then
                startImitation this mirage mirage.mimickingPlayer
        else
            let random = new Random()
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
    
    [<ClientRpc>]
    member this.ImitatePlayerClientRpc(_: ClientRpcParams) =
        let enemy = this.GetComponent<EnemyAI>()
        startImitation this enemy StartOfRound.Instance.localPlayerController