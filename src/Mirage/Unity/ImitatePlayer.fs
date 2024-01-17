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

open FSharpPlus
open FSharpPlus.Data
open GameNetcodeStuff
open Unity.Netcode
open System
open System.Threading
open Mirage.Core.Getter
open Mirage.Core.Logger
open Mirage.Core.Monad
open Mirage.Unity.Audio.AudioStream
open Mirage.Core.Audio.Recording
open Mirage.Core.File

let private get<'A> : Getter<'A> = getter "ImitatePlayer"

/// <summary>
/// A component that can attach to <b>MaskedPlayerEnemy</b> entities and imitate a specific player.
/// </summary>
type ImitatePlayer() =
    inherit NetworkBehaviour()

    let random = new Random()
    let canceller = new CancellationTokenSource()

    let AudioStream: Ref<Option<AudioStream>> = ref None
    let ImitatedPlayer: ref<Option<PlayerControllerB>> = ref None
    
    let getAudioStream = get AudioStream "AudioStream"
    let getImitatedPlayer = get ImitatedPlayer "ImitatedPlayer"

    let rec foobar (this: ImitatePlayer) : Async<Unit> =
        async {
            return! Async.Sleep 1000
            //logInfo "Imitating player"
            let audioStream = this.GetComponent<AudioStream>()
            if not <| audioStream.IsServerRunning() then
                logInfo $"Starting new audio."
                logInfo "Streaming audio"
                audioStream.StreamAudioFromFile $"{RootDirectory}/BepInEx/plugins/asset/ram-ranch.wav"
                logInfo "streaming audio"
                //do! Async.Sleep 200
        }

    let rec runImitationLoop : ResultT<Async<Result<Unit, String>>> =
        monad {
            let methodName = "runImitationLoop"
            let delay = random.Next(1000, 2001) // Play voice every 10-20 secs
            return! liftAsync <| Async.Sleep delay
            let! audioStream = liftResult <| getAudioStream methodName
            let! imitatedPlayer = liftResult <| getImitatedPlayer methodName
            iter audioStream.StreamAudioFromFile <| getRandomRecording random imitatedPlayer
            return! runImitationLoop
        }

    member this.Start() =
        AudioStream.Value <- Some <| this.gameObject.GetComponent<AudioStream>()
        if this.IsHost then
            let enemy = this.gameObject.GetComponent<MaskedPlayerEnemy>()
            ImitatedPlayer.Value <- Some enemy.mimickingPlayer
            toUniTask_ canceller.Token <| foobar this
            //runImitationLoop
            //    |> ResultT.run
            //    |> map handleResult
            //    |> toUniTask_ canceller.Token

    override this.OnDestroy() =
        if this.IsHost then
            logInfo "onDestroy called on ImitatePlayer"
            canceller.Cancel()
            dispose canceller