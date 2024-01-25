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
module Mirage.Unity.Enemy.ImitatePlayer

#nowarn "40"

open System
open FSharpPlus
open Unity.Netcode
open System.Threading
open Mirage.Core.Field
open Mirage.Core.Logger
open Mirage.Core.Monad
open Mirage.Unity.AudioStream
open Mirage.Core.Audio.Recording

/// <summary>
/// A component that can attach to <b>MaskedPlayerEnemy</b> entities and imitate a specific player.
/// </summary>
type ImitatePlayer() =
    inherit NetworkBehaviour()

    let random = new Random()
    let canceller = new CancellationTokenSource()

    let AudioStream: Field<AudioStream>  = field()
    let Mirage: Field<MaskedPlayerEnemy> = field()

    let imitatePlayer (this: ImitatePlayer) =
        ignore <| monad' {
            let! audioStream = getValue AudioStream
            let! mirage = getValue Mirage
            flip iter (getRandomRecording random) <| fun recording ->
                try
                    if this.IsHost then
                        audioStream.StreamAudioFromFile recording
                    else
                        audioStream.UploadAndStreamAudioFromFile(mirage.mimickingPlayer.actualClientId, recording)
                with | error ->
                    logError $"Failed to imitate player: {error}"
        }

    let rec runImitationLoop this =
        async {
            try
                imitatePlayer this
            with | error ->
                logError $"Failed to imitate player: {error}"
            let delay = random.Next(10000, 15001) // Play voice every 10-15 secs
            return! liftAsync <| Async.Sleep delay
            return! runImitationLoop this
        }

    member this.Start() =
        let audioStream = this.gameObject.GetComponent<AudioStream>()
        set AudioStream audioStream
        let audioSource = audioStream.GetAudioSource()
        audioSource.spatialBlend <- 1f
        let mirage = this.gameObject.GetComponent<MaskedPlayerEnemy>()
        setNullable Mirage mirage
        if mirage.mimickingPlayer.actualClientId = GameNetworkManager.Instance.localPlayerController.actualClientId then
            toUniTask_ canceller.Token <| runImitationLoop this

    override _.OnDestroy() =
        canceller.Cancel()
        dispose canceller