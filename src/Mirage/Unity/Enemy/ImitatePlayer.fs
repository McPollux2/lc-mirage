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
open Unity.Netcode
open System.Threading
open Mirage.Core.Field
open Mirage.Core.Logger
open Mirage.Core.Monad
open Mirage.Unity.AudioStream
open Mirage.Unity.RecordingManager

let private get<'A> (field: Field<'A>) = field.Value

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

    let imitatePlayer () =
        ignore <| monad' {
            let! audioStream = get AudioStream
            let! mirage = get Mirage
            let! dissonance = get Dissonance
            //try
            logInfo $"starting mimic audio for player: {getVoiceId dissonance (mirage: MaskedPlayerEnemy).mimickingPlayer}"
            let recording = getRandomRecording dissonance random (mirage: MaskedPlayerEnemy).mimickingPlayer
            iter (audioStream: AudioStream).StreamAudioFromFile recording
            //with | _ -> ()
            //(audioStream: AudioStream).StreamAudioFromFile $"{RootDirectory}/BepInEx/plugins/asset/whistle.wav"
        }

    let rec runImitationLoop =
        async {
            try
                imitatePlayer()
            with | error ->
                logError $"Failed to imitate player: {error}"
            let delay = 10000 // random.Next(10000, 20001) // Play voice every 10-20 secs
            return! liftAsync <| Async.Sleep delay
            return! runImitationLoop
        }

    member this.Start() =
        logInfo "ImitatePlayer#Start is called"
        set Dissonance <| UnityEngine.Object.FindObjectOfType<DissonanceComms>()
        let audioStream = this.gameObject.GetComponent<AudioStream>()
        set AudioStream audioStream
        let audioSource = audioStream.GetAudioSource()
        audioSource.spatialBlend <- 1f
        //let lowPassFilter = audioSource.gameObject.AddComponent<AudioLowPassFilter>()
        //lowPassFilter.cutoffFrequency <- 20000f
        let mirage = this.gameObject.GetComponent<MaskedPlayerEnemy>()
        set Mirage mirage
        if this.IsHost then
            toUniTask_ canceller.Token runImitationLoop
            ignore imitatePlayer

    override this.OnDestroy() =
        if this.IsHost then
            canceller.Cancel()
            dispose canceller