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

open Dissonance
open FSharpPlus
open FSharpPlus.Data
open Unity.Netcode
open System.Threading
open UnityEngine
open Mirage.Core.File
open Mirage.Core.Field
open Mirage.Core.Logger
open Mirage.Core.Monad
open Mirage.Unity.AudioStream.Component
open Mirage.Unity.RecordingManager

let private get<'A> : Getter<'A> = getter "ImitatePlayer"

/// <summary>
/// A component that can attach to <b>MaskedPlayerEnemy</b> entities and imitate a specific player.
/// </summary>
type ImitatePlayer() =
    inherit NetworkBehaviour()

    let random = new System.Random()
    let canceller = new CancellationTokenSource()

    let Dissonance = ref None
    let AudioStream = ref None
    let Mirage = ref None
    
    let getDissonance = get Dissonance "Dissonance"
    let getAudioStream = get AudioStream "AudioStream"
    let getMirage = get Mirage "Mirage"

    let rec runImitationLoop =
        monad {
            let methodName = "runImitationLoop"
            let delay = random.Next(10000, 20001) // Play voice every 10-20 secs
            return! liftAsync <| Async.Sleep delay
            let! audioStream = liftResult <| getAudioStream methodName
            let! mirage = liftResult <| getMirage methodName
            let! dissonance = liftResult <| getDissonance methodName
            //let recording = getRandomRecording dissonance random (mirage: MaskedPlayerEnemy).mimickingPlayer
            //iter (audioStream: AudioStream).StreamAudioFromFile recording
            (audioStream: AudioStream).StreamAudioFromFile $"{RootDirectory}/BepInEx/plugins/asset/whistle.wav"
            return! runImitationLoop
        }

    member this.Start() =
        set Dissonance <| UnityEngine.Object.FindObjectOfType<DissonanceComms>()
        let audioStream = this.gameObject.GetComponent<AudioStream>()
        set AudioStream audioStream
        let audioSource = audioStream.GetAudioSource()
        audioSource.spatialBlend <- 1f
        let lowPassFilter = audioSource.gameObject.AddComponent<AudioLowPassFilter>()
        lowPassFilter.cutoffFrequency <- 20000f
        let mirage = this.gameObject.GetComponent<MaskedPlayerEnemy>()
        set Mirage mirage
        if this.IsHost then
            runImitationLoop
                |> ResultT.run
                |> map handleResult
                |> toUniTask_ canceller.Token

    override this.OnDestroy() =
        if this.IsHost then
            canceller.Cancel()
            dispose canceller