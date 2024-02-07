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
module Mirage.Unity.MimicVoice

open Unity.Netcode
open FSharpPlus
open GameNetcodeStuff
open System
open System.Threading
open Mirage.Core.Field
open Mirage.Core.Logger
open Mirage.Core.Monad
open Mirage.Core.Audio.Recording
open Mirage.Core.Config
open Mirage.Unity.AudioStream
open Mirage.Unity.MimicPlayer

let private get<'A> = getter<'A> "MimicVoice"

/// <summary>
/// A component that attaches to an <b>EnemyAI</b> to mimic a player's voice.
/// </summary>
type MimicVoice() =
    inherit NetworkBehaviour()

    let canceller = new CancellationTokenSource()
    let random = new Random()

    let AudioStream = field<AudioStream>()
    let EnemyAI = field()
    let getAudioStream = get AudioStream "AudioStream"
    let getEnemyAI = get EnemyAI "EnemyAI"

    /// <summary>
    /// Mimic the voice of the local player's voice (for all players).
    /// </summary>
    let mimicLocalVoice enemy =
        let streamLocalVoice () =
            handleResult <| monad' {
                let! audioStream = getAudioStream "mimicVoice"
                let player = StartOfRound.Instance.localPlayerController
                flip iter (getRandomRecording random) <| fun recording ->
                    try
                        if player.playerClientId = 0UL then
                            audioStream.StreamAudioFromFile recording
                        else
                            audioStream.UploadAndStreamAudioFromFile(
                                player.actualClientId,
                                recording
                            )
                    with | error ->
                        logError $"Failed to mimic voice: {error}"
            }
        let rec runMimicLoop (enemyAI: EnemyAI) =
            async {
                streamLocalVoice()
                let config = getConfig()
                let delay = 
                    if enemyAI :? MaskedPlayerEnemy then
                        random.Next(config.imitateMinDelay, config.imitateMaxDelay + 1)
                    else
                        random.Next(config.imitateMinDelayNonMasked, config.imitateMaxDelayNonMasked + 1)
                return! liftAsync <| Async.Sleep delay
                return! runMimicLoop enemy
            }
        toUniTask_ canceller.Token <| runMimicLoop enemy

    member this.Awake() =
        let audioStream = this.gameObject.GetComponent<AudioStream>()
        set AudioStream audioStream
        let enemyAI = this.gameObject.GetComponent<EnemyAI>()
        setNullable EnemyAI enemyAI
    
    member this.Start() =
        if this.IsHost then
            let mimicPlayer = this.gameObject.GetComponent<MimicPlayer>()
            iter this.StartVoiceMimic <| mimicPlayer.GetMimickingPlayer()

    override _.OnDestroy() =
        try canceller.Cancel()
        with | _ -> ()
        dispose canceller

    /// <summary>
    /// Begin mimicking the player's voice.<br />
    /// Note: This can only be invoked by the host.
    /// </summary>
    member this.StartVoiceMimic(player: PlayerControllerB) =
        handleResult <| monad' {
            if not this.IsHost then
                return! Error "MimicVoice#StartVoiceMimic can only be invoked by the host."
            let! enemyAI = getEnemyAI "StartVoiceMimic"
            if player.playerClientId = 0UL then
                mimicLocalVoice enemyAI
            else
                this.StartVoiceMimicClientRpc <|
                    new ClientRpcParams(
                        Send = new ClientRpcSendParams(TargetClientIds = [|player.actualClientId|])
                    )
        }

    [<ClientRpc>]
    member this.StartVoiceMimicClientRpc(_: ClientRpcParams) =
        handleResult <| monad' {
            if not <| this.IsHost then
                let! enemyAI = getEnemyAI "StartVoiceMimicClientRpc"
                mimicLocalVoice enemyAI
        }