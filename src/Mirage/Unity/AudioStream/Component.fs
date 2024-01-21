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
module Mirage.Unity.AudioStream.Component

open FSharpPlus
open UnityEngine
open Unity.Netcode
open Cysharp.Threading.Tasks
open NAudio.Wave
open System.Threading
open Microsoft.FSharp.Data.UnitSystems.SI.UnitNames
open Mirage.Core.Logger
open Mirage.Core.Audio.Data
open Mirage.Core.Monad
open Mirage.Unity.Network
open Mirage.Core.Field
open Mirage.Core.Audio.Format
open Mirage.Unity.AudioStream.Server
open Mirage.Unity.AudioStream.Client

let [<Literal>] ClientTimeout = 30<second>
let private get<'A> : Getter<'A> = getter "AudioStream"

/// <summary>
/// A component that allows an entity to stream audio to a client, playing the audio back live.
/// </summary>
type AudioStream() =
    inherit NetworkBehaviour()

    let AudioSource: ref<Option<AudioSource>> = ref None
    let AudioServer: ref<Option<AudioServer>> = ref None
    let AudioClient: ref<Option<AudioClient>> = ref None

    let getAudioSource = get AudioSource "AudioSource"
    let getAudioServer = get AudioServer "AudioServer"
    let getAudioClient = get AudioClient "AudioClient"

    let stopAudioServer() =
        iter stopServer AudioServer.Value
        setNone AudioServer

    let stopAudioClient() =
        iter stopClient AudioClient.Value
        setNone AudioClient

    let stopAll () =
        flip iter AudioSource.Value <| fun audioSource ->
            UnityEngine.Object.Destroy <| audioSource.clip
        stopAudioServer()
        stopAudioClient()

    member this.Awake() =
        let audioSource = this.gameObject.AddComponent<AudioSource>()
        audioSource.dopplerLevel <- 0f
        audioSource.maxDistance <- 50f
        audioSource.minDistance <- 6f
        audioSource.priority <- 0
        audioSource.spread <- 30f
        set AudioSource audioSource

    override _.OnDestroy() = stopAll()

    /// <summary>
    /// Get the attached audio source.
    /// </summary>
    member _.GetAudioSource() =
        match AudioSource.Value with
            | None -> invalidOp "AudioStream#GetAudioSource called while AudioSource has not been initialized yet."
            | Some audio -> audio

    /// <summary>
    /// Stream the given audio file to all clients. This can only be invoked by the host.
    /// </summary>
    member this.StreamAudioFromFile(filePath: string) =
        if not this.IsHost then invalidOp "This method can only be invoked by the host."
        stopAudioServer()
        let canceller = new CancellationTokenSource()
        toUniTask_ canceller.Token <| async {
            // Stream the audio to all clients.
            let! (audioServer, pcmHeader) = startServer this.SendFrameClientRpc this.FinishAudioClientRpc filePath
            set AudioServer audioServer
            this.InitializeAudioClientRpc pcmHeader

            // Play the audio directly for the host.
            let! audioClip =
                forkReturn <|
                    async {
                        use audioReader = new WaveFileReader(filePath)
                        return convertToAudioClip audioReader
                    }
            handleResult <| monad' {
                let! audioSource = getAudioSource "StreamAudioFromFile"
                audioSource.Stop()
                UnityEngine.Object.Destroy <| audioSource.clip
                audioSource.clip <- audioClip
                audioSource.Play()
            }
        }

    /// <summary>
    /// Initialize the audio client by sending it the required pcm header.
    /// </summary>
    [<ClientRpc>]
    member this.InitializeAudioClientRpc(pcmHeader: PcmHeader) =
        handleResultWith stopAll <| monad' {
            if not this.IsHost then
                stopAudioClient()
                let! audioSource = getAudioSource "InitializeAudioClientRpc"
                let client = startClient audioSource pcmHeader
                set AudioClient client
                startTimeout client ClientTimeout
                    |> _.AsUniTask().Forget()
                this.InitializeAudioServerRpc <| new ServerRpcParams()
        }

    /// <summary>
    /// Begin broadcasting audio to all clients.
    /// </summary>
    [<ServerRpc(RequireOwnership = false)>]
    member this.InitializeAudioServerRpc(serverParams: ServerRpcParams) =
        handleResultWith stopAll <| monad' {
            if this.IsHost && isValidClient this serverParams then
                let! audioServer = getAudioServer "InitializeAudioServerRpc"
                broadcastAudio audioServer
        }

    /// <summary>
    /// Send audio frame data to the client to play.
    /// </summary>
    [<ClientRpc(Delivery = RpcDelivery.Unreliable)>]
    member this.SendFrameClientRpc(frameData: FrameData) =
        handleResult <| monad' {
            if not this.IsHost then
                let! audioClient = getAudioClient "SendFrameClientRpc"
                setFrameData audioClient frameData 
        }

    /// <summary>
    /// Called when audio is finished streaming.<br />
    /// This disables the client timeout to allow it to continue playing all the audio it already has.
    /// </summary>
    [<ClientRpc>]
    member _.FinishAudioClientRpc() = iter stopTimeout AudioClient.Value