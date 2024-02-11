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
open System
open System.Threading
open Mirage.Core.Field
open Mirage.Core.Logger
open Mirage.Core.Monad
open Mirage.Core.Audio.Recording
open Mirage.Core.Config
open Mirage.Unity.AudioStream
open Mirage.Unity.MimicPlayer

#nowarn "40"

let private get<'A> = getter<'A> "MimicVoice"

/// <summary>
/// A component that attaches to an <b>EnemyAI</b> to mimic a player's voice.
/// </summary>
type MimicVoice() as self =
    inherit NetworkBehaviour()

    let random = new Random()

    let MimicPlayer = field<MimicPlayer>()
    let AudioStream = field<AudioStream>()
    let EnemyAI = field()
    let getMimicPlayer = get MimicPlayer "MimicPlayer"
    let getAudioStream = get AudioStream "AudioStream"
    let getEnemyAI = get EnemyAI "EnemyAI"

    let startVoiceMimic (enemyAI: EnemyAI) =
        let rec runMimicLoop =
            let config = getConfig()
            let mimicVoice () =
                handleResult <| monad' {
                    let methodName = "mimicVoice"
                    let! mimicPlayer = getMimicPlayer methodName
                    let! audioStream = getAudioStream methodName
                    ignore <| monad' {
                        let! player = mimicPlayer.GetMimickingPlayer()
                        let! recording = getRandomRecording random
                        try
                            if player.IsHost && player.playerClientId = 0UL then
                                audioStream.StreamAudioFromFile recording
                            else if player = StartOfRound.Instance.localPlayerController then
                                audioStream.UploadAndStreamAudioFromFile(
                                    player.actualClientId,
                                    recording
                                )
                        with | error ->
                            logError $"Failed to mimic voice: {error}"
                    }
                }
            let delay =
                if enemyAI :? MaskedPlayerEnemy then
                    random.Next(config.imitateMinDelay, config.imitateMaxDelay + 1)
                else
                    random.Next(config.imitateMinDelayNonMasked, config.imitateMaxDelayNonMasked + 1)
            async {
                mimicVoice()
                return! Async.Sleep delay
                return! runMimicLoop
            }
        toUniTask_ self.destroyCancellationToken runMimicLoop

    member this.Awake() =
        set MimicPlayer <| this.gameObject.GetComponent<MimicPlayer>()
        set AudioStream <| this.gameObject.GetComponent<AudioStream>()
        setNullable EnemyAI <| this.gameObject.GetComponent<EnemyAI>()
    
    member _.Start() =
        handleResult <| monad' {
            let! enemyAI = getEnemyAI "Start"
            startVoiceMimic enemyAI
        }