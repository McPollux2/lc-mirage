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
module Mirage.Unity.AudioStream.Server

open NAudio.Wave
open FSharpPlus
open FSharpx.Control
open System
open System.Threading
open Mirage.Core.Audio.Data
open Mirage.Core.Logger
open Mirage.Core.Audio.Stream
open Mirage.Core.Monad
open Mirage.Core.Audio.Format

// The amount of time the channel should block before it exits.
let [<Literal>] ChannelTimeout = 30_000 // 30 seconds.

/// <summary>
/// Handles the server side of audio streaming.
/// </summary>
type AudioServer =
    private
        {   sendFrame: FrameData -> Unit
            onFinish: Unit -> Unit
            audioReader: Mp3FileReader
            channel: BlockingQueueAgent<Option<FrameData>>
            canceller: CancellationTokenSource
            mutable stopped: bool
        }

/// <summary>
/// Stop the audio server. This must be called to cleanup resources.
/// </summary>
let stopServer (server: AudioServer) =
    if not server.stopped then
        server.stopped <- true
        server.canceller.Cancel()
        dispose server.canceller
        dispose server.audioReader.mp3Stream
        dispose server.audioReader
        dispose server.channel
        server.onFinish()

/// <summary>
/// Start the audio server. This does not begin broadcasting audio.
/// </summary>
/// <param name="sendFrame">
/// The RPC method for sending frame data to all clients.
/// </param>
/// <param name="filePath">
/// Source audio to stream from, supporting only <b>.wav</b> audio files.
/// </param>
let startServer (sendFrame: FrameData -> Unit) (onFinish: Unit -> Unit) (filePath: string) : Async<AudioServer * PcmHeader> =
    async {
        let! audioReader =
            forkReturn <|
                async {
                    use audioReader = new AudioFileReader(filePath)
                    return convertToMp3 audioReader
                }
        let server =
            {   sendFrame = sendFrame
                onFinish = onFinish
                audioReader = audioReader
                channel = new BlockingQueueAgent<Option<FrameData>>(Int32.MaxValue)
                canceller = new CancellationTokenSource()
                stopped = false
            }
        let pcmHeader = getPcmHeader audioReader
        return (server, pcmHeader)
    }

/// <summary>
/// Begin broadcasting audio to all clients.
/// </summary>
let broadcastAudio (server: AudioServer) : Unit =
    // The "producer" processes the audio frames from a separate thread, and passes it onto the consumer.
    let producer =
        async {
            try
                return!
                    streamAudio server.audioReader <| fun frameData ->
                        server.channel.AsyncAdd(frameData, ChannelTimeout)
            with | error ->
                logError $"AudioServer producer caught an exception: {error}"
                stopServer server
        }

    // The "consumer" reads the processed audio frames and then runs the sendFrame function.
    let rec consumer frameData =
        async {
            match frameData with
                | None -> stopServer server
                | Some frame ->
                    server.sendFrame frame
                    return! consumer =<< server.channel.AsyncGet ChannelTimeout
        }

    // Start the producer on a separate thread.
    Async.Start(producer, server.canceller.Token)

    // Start the consumer in the current thread.
    try
        server.channel.AsyncGet ChannelTimeout
            >>= consumer
            |> toUniTask_ server.canceller.Token
    with | error ->
        logError $"AudioServer consumer caught an exception: {error}"
        stopServer server

/// <summary>
/// Whether the server is currently running or not.
/// </summary>
let isRunning (server: AudioServer) = not server.stopped