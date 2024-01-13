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
module Mirage.Unity.Audio.Network.Server

open NAudio.Wave
open FSharpPlus
open FSharpx.Control
open System
open System.Threading
open Cysharp.Threading.Tasks
open Mirage.Core.Audio.Data
open Mirage.Core.Logger
open Mirage.Core.Audio.Network.Stream

// The amount of time the channel should block before it exits.
let [<Literal>] ChannelTimeout = 30_000 // 30 seconds.

/// <summary>
/// Handles the server side of audio streaming.
/// </summary>
type AudioServer =
    { sendFrame: FrameData -> Unit
      audioReader: Mp3FileReader
      channel: BlockingQueueAgent<Option<FrameData>>
      canceller: CancellationTokenSource
    }

/// <summary>
/// Stop the audio server. This must be called to cleanup resources.
/// </summary>
let stopServer (server: AudioServer) =
    server.canceller.Cancel()
    dispose server.canceller
    dispose server.audioReader
    dispose server.channel

/// <summary>
/// Start streaming audio to clients.
/// </summary>
let startServer (sendFrame: FrameData -> Unit) (audioReader: Mp3FileReader) : AudioServer =
    let channel = new BlockingQueueAgent<Option<FrameData>>(Int32.MaxValue)
    let canceller = new CancellationTokenSource()
    let server =
        {   sendFrame = sendFrame
            audioReader = audioReader
            channel = channel
            canceller = canceller
        }

    // The "producer" processes the audio frames from a separate thread, and passes it onto the consumer.
    // If any exceptions are found, AudioReader is disposed.
    let producer =
        async {
            try
                return!
                    streamAudio audioReader <| fun frameData ->
                        channel.AsyncAdd(frameData, ChannelTimeout)
            with | error ->
                logError $"AudioServer producer thread caught an exception: {error.Message}"
                dispose audioReader
        }

    // The "consumer" reads the processed audio frames and then runs the sendFrame function.
    let rec consumer frameData =
        async {
            match frameData with
                | None -> stopServer server
                | Some frame ->
                    sendFrame frame
                    return! consumer =<< channel.AsyncGet ChannelTimeout
        }

    // Start the producer on a separate thread.
    Async.Start(producer, canceller.Token)

    // Start the consumer in the current thread.
    try
        let toTask async = Async.StartImmediateAsTask(async, canceller.Token)
        channel.AsyncGet ChannelTimeout
            >>= consumer
            |> toTask
            |> _.AsUniTask().Forget()
    with | error ->
        logError $"AudioServer consumed caught an exception: {error.Message}"
        stopServer server
    
    server