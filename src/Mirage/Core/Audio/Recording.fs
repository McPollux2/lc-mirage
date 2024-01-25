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
module Mirage.Core.Audio.Recording

#nowarn "40"

open FSharpPlus
open FSharpx.Control
open Dissonance.Audio
open UnityEngine
open System
open System.IO
open System.Threading
open Dissonance

/// <summary>
/// The directory to save audio files in.
/// </summray>
let private RecordingDirectory = $"{Application.dataPath}/../Mirage"

/// <summary>
/// A recording file for the local player.<br />
/// This writes to the file on a separate thread to avoid stalling the unity thread.
/// </summary>
type Recording =
    private
        {   audioWriter: AudioFileWriter
            channel: BlockingQueueAgent<array<float32>>
            canceller: CancellationTokenSource
        }

/// <summary>
/// Create a recording file with a random name.
/// </summary>
let createRecording format =
    let filePath = $"{RecordingDirectory}/{DateTime.UtcNow.ToFileTime()}.wav"
    let recording =
        {   audioWriter = new AudioFileWriter(filePath, format)
            channel = new BlockingQueueAgent<array<float32>>(Int32.MaxValue)
            canceller = new CancellationTokenSource()
        }
    //let rec consumer =
    //    async {
    //        let! samples = recording.channel.AsyncGet()
    //        recording.audioWriter.WriteSamples <| new ArraySegment<float32>(samples)
    //        return! consumer
    //    }
    //Async.Start(consumer, recording.canceller.Token)
    recording

/// <summary>
/// Dispose resources associated with the recording.
/// </summary>
let disposeRecording (recording: Recording) =
    recording.canceller.Cancel()
    dispose recording.canceller
    dispose recording.audioWriter
    dispose recording.channel

/// <summary>
/// Whether or not samples should still be recorded.<br />
/// If false, the recording should be disposed.
/// </summary>
let isRecording (dissonance: DissonanceComms) =
    not (isNull GameNetworkManager.Instance)
        && not (isNull GameNetworkManager.Instance.localPlayerController)
        && not GameNetworkManager.Instance.localPlayerController.isPlayerDead
        && IngamePlayerSettings.Instance.Settings.pushToTalk
        && not dissonance.IsMuted

/// <summary>
/// Write the samples into the recording file.
/// </summary>
let writeRecording (recording: Recording) (samples: array<float32>) =
    //recording.channel.Add samples
    recording.audioWriter.WriteSamples <| new ArraySegment<float32>(samples)

/// <summary>
/// Delete the recordings of the local player.
/// </summary>
let deleteRecordings () = Directory.Delete(RecordingDirectory, true)

/// <summary>
/// Get a random recording's file path. If no recordings exist, this will return <b>None</b>.
/// </summary>
let getRandomRecording (random: Random) =
    let recordings =
        try Directory.GetFiles RecordingDirectory
        with | _ -> zero
    if recordings.Length = 0 then None
    else Some recordings[random.Next recordings.Length]