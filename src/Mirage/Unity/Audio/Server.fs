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
module Mirage.Unity.Audio.Server

open NAudio.Wave
open FSharpPlus
open FSharpx.Control
open System
open System.Threading
open Mirage.Core.Audio.Data
open Mirage.Core.Audio.Stream
open Mirage.Core.Logger
open Cysharp.Threading.Tasks

/// <summary>
/// The amount of time the channel should block before it exits.
/// </summary>
let [<Literal>] ChannelTimeout = 30_000 // 30 seconds.

/// <summary>
/// This is meant to be used within the <b>AudioStream</b> component.
/// Each instance of the component should have its own instance of this class.
/// </summary>
type AudioServer(audioReader: Mp3FileReader, sendFrame: FrameData -> Unit) =
    let channel = new BlockingQueueAgent<Option<FrameData>>(Int32.MaxValue)
    let canceller = new CancellationTokenSource()

    /// <summary>
    /// Start the audio stream.
    /// </summary>
    member this.StartStream() =
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
                    | None -> dispose this
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
            dispose this

    interface IDisposable with
        member _.Dispose() =
            dispose audioReader
            dispose channel
            dispose canceller