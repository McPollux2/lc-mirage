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
open Unity.Netcode
open System
open System.Threading
open UnityEngine
open Mirage.Core.Getter
open Mirage.Core.Logger
open Mirage.Core.Monad
open Mirage.Core.File
open Mirage.core.Recording
open Mirage.Unity.AudioStream.Component
open Network

let private get<'A> : Getter<'A> = getter "ImitatePlayer"

/// <summary>
/// A component that can attach to <b>MaskedPlayerEnemy</b> entities and imitate a specific player.
/// </summary>
type ImitatePlayer() =
    inherit NetworkBehaviour()

    let random = new System.Random()
    let canceller = new CancellationTokenSource()

    let AudioStream: Ref<Option<AudioStream>> = ref None
    let Mirage: Ref<Option<MaskedPlayerEnemy>> = ref None
    
    let getAudioStream = get AudioStream "AudioStream"
    let getMirage = get Mirage "Mirage"

    let rec foobar (this: ImitatePlayer) : Async<Unit> =
        async {
            let audioStream = this.GetComponent<AudioStream>()
            audioStream.StreamAudioFromFile $"{RootDirectory}/BepInEx/plugins/asset/ram-ranch.wav"
            //do! Async.Sleep 4000
            //return! foobar this
        }

    let rec runImitationLoop : ResultT<Async<Result<Unit, String>>> =
        monad {
            let methodName = "runImitationLoop"
            let delay = random.Next(1000, 2001) // Play voice every 10-20 secs
            return! liftAsync <| Async.Sleep delay
            let! audioStream = liftResult <| getAudioStream methodName
            let! mirage = liftResult <| getMirage methodName
            iter audioStream.StreamAudioFromFile <| getRandomRecording random mirage.mimickingPlayer
            return! runImitationLoop
        }

    member this.Start() =
        let audioStream = this.gameObject.GetComponent<AudioStream>()
        AudioStream.Value <- Some audioStream
        let audioSource = audioStream.AttachedAudioSource
        audioSource.spatialBlend <- 1f
        let lowPassFilter = audioSource.gameObject.AddComponent<AudioLowPassFilter>()
        lowPassFilter.cutoffFrequency <- 20000f
        let mirage = this.gameObject.GetComponent<MaskedPlayerEnemy>()
        Mirage.Value <- Some mirage
        if this.IsHost then
            toUniTask_ canceller.Token <| foobar this
            //runImitationLoop
            //    |> ResultT.run
            //    |> map handleResult
            //    |> toUniTask_ canceller.Token
        else this.SyncPlayerSuitServerRpc <| new ServerRpcParams()

    override this.OnDestroy() =
        if this.IsHost then
            canceller.Cancel()
            dispose canceller

    [<ServerRpc(RequireOwnership = false)>]
    member this.SyncPlayerSuitServerRpc(serverParams: ServerRpcParams) =
        handleResult <| monad {
            if this.IsHost && isValidClient this serverParams then
                let! mirage = getMirage "SyncPlayerSuitServerRpc"
                let suitId = mirage.mimickingPlayer.currentSuitID
                let clientId = serverParams.Receive.SenderClientId
                let sendParams = new ClientRpcSendParams(TargetClientIds = [clientId])
                let clientParams = new ClientRpcParams(Send = sendParams)
                this.SyncPlayerSuitClientRpc(suitId, clientParams)
            }


    [<ClientRpc>]
    member this.SyncPlayerSuitClientRpc(suitId: int, _: ClientRpcParams) =
        handleResult <| monad {
            if not this.IsHost then
                let! mirage = getMirage "SyncPlayerSuitClientRpc"
                mirage.SetSuit suitId
        }