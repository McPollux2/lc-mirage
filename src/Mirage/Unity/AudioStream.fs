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
open Mirage.Core.Getter
open Mirage.Core.Audio.Data
open Mirage.Core.Audio.Network.Server
open Mirage.Core.Audio.Network.Client
open Mirage.Unity.NetworkBehaviour

let [<Literal>] ClientTimeout = 30<second>
let get<'A> : Getter<'A> = getter "AudioStream"

/// <summary>
/// A component that allows an entity to stream audio to a client, playing the audio back live.
/// </summary>
type AudioStream() =
    inherit NetworkBehaviour()

    let mutable AudioSource: ref<Option<AudioSource>> = ref None
    let mutable AudioServer: ref<Option<AudioServer>> = ref None
    let mutable AudioClient: ref<Option<AudioClient>> = ref None

    let getAudioSource = get AudioSource "AudioSource"
    let getAudioServer = get AudioServer "AudioServer"
    let getAudioClient = get AudioClient "AudioClient"

    let stopAudioServer() =
        iter stopServer AudioServer.Value
        AudioServer <- ref None

    let stopAudioClient() =
        iter stopClient AudioClient.Value
        AudioClient.Value <- None

    let stopAll () =
        stopAudioServer()
        stopAudioClient()

    member this.Awake() =
        AudioSource <- ref << Some <| this.gameObject.AddComponent<AudioSource>()

    override _.OnDestroy() = stopAll()

    /// <summary>
    /// Whether the server is is running or not (broadcasting audio to clients).<br />
    /// This can only be invoked by the host.
    /// </summary>
    member _.IsServerRunning() = fold (konst isRunning) false AudioServer.Value

    /// <summary>
    /// Stream the given audio file to all clients. This can only be invoked by the host.
    /// </summary>
    member this.StreamAudioFromFile(filePath: string) =
        if not this.IsHost then invalidOp "This method can only be invoked by the host."
        stopAudioServer()
        let audioReader = new Mp3FileReader(filePath)
        AudioServer <- ref << Some <| startServer this.SendFrameClientRpc this.FinishAudioClientRpc audioReader
        this.InitializeAudioClientRpc <| getPcmHeader audioReader

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
                AudioClient.Value <- Some client
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