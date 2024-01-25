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
module Mirage.Core.Audio.Network.Sender

open NAudio.Wave
open FSharpPlus
open FSharpx.Control
open System
open System.Threading
open Stream
open Mirage.Core.Audio.Data
open Mirage.Core.Logger
open Mirage.Core.Monad

// The amount of time the channel should block before it exits.
let [<Literal>] ChannelTimeout = 30_000 // 30 seconds.

/// <summary>
/// Send audio to all <b>AudioReceiver</b>s.
/// </summary>
type AudioSender =
    private
        {   sendFrame: FrameData -> Unit
            onFinish: Unit -> Unit
            audioReader: Mp3FileReader
            channel: BlockingQueueAgent<Option<FrameData>>
            canceller: CancellationTokenSource
            mutable stopped: bool
        }

/// <summary>
/// Stop the audio sender. This must be called to cleanup resources.
/// </summary>
let stopSender (sender: AudioSender) =
    if not sender.stopped then
        sender.stopped <- true
        sender.canceller.Cancel()
        dispose sender.canceller
        dispose sender.audioReader.mp3Stream
        dispose sender.audioReader
        dispose sender.channel
        sender.onFinish()

/// <summary>
/// Start the audio sender. This does not begin broadcasting audio.
/// </summary>
/// <param name="sendFrame">
/// The RPC method for sending frame data to all clients.
/// </param>
/// <param name="filePath">
/// Source audio to stream from, supporting only <b>.wav</b> audio files.
/// </param>
let startSender (sendFrame: FrameData -> Unit) (onFinish: Unit -> Unit) (audioReader: Mp3FileReader) : AudioSender * PcmHeader =
    let sender =
        {   sendFrame = sendFrame
            onFinish = onFinish
            audioReader = audioReader
            channel = new BlockingQueueAgent<Option<FrameData>>(Int32.MaxValue)
            canceller = new CancellationTokenSource()
            stopped = false
        }
    let pcmHeader = getPcmHeader audioReader
    (sender, pcmHeader)

/// <summary>
/// Begin sending audio.
/// </summary>
let sendAudio (sender: AudioSender) : Unit =
    // The "producer" processes the audio frames from a separate thread, and passes it onto the consumer.
    let producer =
        async {
            try
                return!
                    streamAudio sender.audioReader <| fun frameData ->
                        sender.channel.AsyncAdd(frameData, ChannelTimeout)
            with | error ->
                logError $"AudioSender producer caught an exception: {error}"
                stopSender sender
        }

    // The "consumer" reads the processed audio frames and then runs the sendFrame function.
    let rec consumer frameData =
        async {
            match frameData with
                | None -> stopSender sender
                | Some frame ->
                    sender.sendFrame frame
                    return! consumer =<< sender.channel.AsyncGet ChannelTimeout
        }

    // Start the producer on a separate thread.
    Async.Start(producer, sender.canceller.Token)

    // Start the consumer in the current thread.
    try
        sender.channel.AsyncGet ChannelTimeout
            >>= consumer
            |> toUniTask_ sender.canceller.Token
    with | error ->
        logError $"AudioSender consumer caught an exception: {error}"
        stopSender sender

/// <summary>
/// Whether the sender is currently running or not.
/// </summary>
let isRunning (sender: AudioSender) = not sender.stopped