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
module Mirage.Unity.Audio.AudioStream

open FSharpPlus
open NAudio.Wave
open UnityEngine
open Unity.Netcode
open Cysharp.Threading.Tasks
open Microsoft.FSharp.Data.UnitSystems.SI.UnitNames
open Mirage.Core.Logger
open Mirage.Core.Audio.Data
open Mirage.Core.Audio.Network.Server
open Mirage.Core.Audio.Network.Client
open Mirage.Unity.NetworkBehaviour

let [<Literal>] ClientTimeout = 30<second>

/// <summary>
/// A component that allows an entity to stream audio to a client, playing the audio back live.
/// </summary>
type AudioStream() =
    inherit NetworkBehaviour()

    let mutable audioSource: Option<AudioSource> = None
    let mutable audioServer: Option<AudioServer> = None
    let mutable audioClient: Option<AudioClient> = None

    let stopAudioServer() =
        iter stopServer audioServer
        audioServer <- None

    let stopAudioClient() =
        iter stopClient audioClient
        audioClient <- None

    let stopAll () =
        stopAudioServer()
        stopAudioClient()

    member private this.Awake() =
        audioSource <- Some <| this.gameObject.AddComponent<AudioSource>()

    override _.OnDestroy() = stopAll()

    /// <summary>
    /// Stream the given audio file to all clients. This can only be invoked by the host.
    /// </summary>
    member this.StreamAudioFromFile(filePath: string) =
        if not this.IsHost then invalidOp "This method can only be invoked by the host."
        stopAudioServer()
        let audioReader = new Mp3FileReader(filePath)
        audioServer <- Some <| startServer this.SendFrameClientRpc audioReader
        this.InitializeAudioClientRpc <| getPcmHeader audioReader

    /// <summary>
    /// Initialize the audio client by sending it the required pcm header.
    /// </summary>
    [<ClientRpc>]
    member this.InitializeAudioClientRpc(pcmHeader: PcmHeader) =
        handleErrorWith stopAll <| monad' {
            if not this.IsHost then
                stopAudioClient()
                let! source =
                    Option.toResultWith
                        "AudioSource has not been initialized yet. This is unexpected, as it should be initialized in AudioStream#Awake."
                        audioSource
                let client = startClient source pcmHeader
                audioClient <- Some client
                startTimeout client ClientTimeout
                    |> _.AsUniTask().Forget()
                this.InitializeAudioServerRpc <| new ServerRpcParams()
        }

    /// <summary>
    /// Begin broadcasting audio to all clients.
    /// </summary>
    [<ServerRpc(RequireOwnership = false)>]
    member this.InitializeAudioServerRpc(serverParams: ServerRpcParams) =
        handleErrorWith stopAll <| monad' {
            if this.IsHost && isValidClient this serverParams then
                let! server =
                    Option.toResultWith
                        "AudioStream#InitializeAudioServerRpc was called while AudioServer has not started yet."
                        audioServer
                broadcastAudio server
        }

    /// <summary>
    /// Send audio frame data to the client to play.
    /// </summary>
    [<ClientRpc(Delivery = RpcDelivery.Unreliable)>]
    member this.SendFrameClientRpc(frameData: FrameData) =
        handleError <| monad' {
            if not this.IsHost then
                let! client =
                    Option.toResultWith
                        "AudioStream#SendFrameClientRpc was called while AudioClient has not started yet."
                        audioClient
                setFrameData client frameData 
        }