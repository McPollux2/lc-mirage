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
open Dissonance.Audio
open UnityEngine
open System
open System.IO
open Dissonance

/// <summary>
/// The directory to save audio files in.
/// </summray>
let private RecordingDirectory = $"{Application.dataPath}/../Mirage"

/// <summary>
/// Create a recording file with a random name.
/// </summary>
let createRecording format =
    let filePath = $"{RecordingDirectory}/{DateTime.UtcNow.ToFileTime()}.wav"
    let recording = new AudioFileWriter(filePath, format)
    (filePath, recording)

/// <summary>
/// Whether or not samples should still be recorded.<br />
/// If false, the recording should be disposed.
/// </summary>
let isRecording (dissonance: DissonanceComms) (speechDetected : bool) =
    let isPlayerDead =
        not (isNull GameNetworkManager.Instance)
            && not (isNull GameNetworkManager.Instance.localPlayerController)
            && not GameNetworkManager.Instance.localPlayerController.isPlayerDead
    let pushToTalkEnabled = IngamePlayerSettings.Instance.settings.pushToTalk
    let pushToTalkPressed = pushToTalkEnabled && not dissonance.IsMuted
    let voiceActivated = not pushToTalkEnabled && speechDetected
    isPlayerDead && (pushToTalkPressed || voiceActivated)

/// <summary>
/// Delete the recordings of the local player. Any exception found is ignored.
/// </summary>
let deleteRecordings () =
    try Directory.Delete(RecordingDirectory, true)
    with | _ -> ()

/// <summary>
/// Get a random recording's file path. If no recordings exist, this will return <b>None</b>.
/// </summary>
let getRandomRecording (random: Random) =
    let recordings =
        try Directory.GetFiles RecordingDirectory
        with | _ -> zero
    if recordings.Length = 0 then None
    else Some recordings[random.Next recordings.Length]