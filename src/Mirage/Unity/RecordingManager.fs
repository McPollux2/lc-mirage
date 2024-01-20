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
module Mirage.Unity.RecordingManager

open Dissonance
open PlayerManager
open GameNetcodeStuff
open System
open System.IO
open FSharpPlus
open Mirage.Core.File
open Mirage.Core.Logger

/// <summary>
/// Used for saving recordings via dissonance.
/// </summary>
type RecordingManager =
    {   playerManager: PlayerManager<string>
        dissonance: DissonanceComms
    }

/// <summary>
/// Retrieve the player's voice id. If the player isn't initialized, this will return <b>None</b>.
/// </summary>
let getVoiceId (dissonance: DissonanceComms) (player: PlayerControllerB) : Option<string> =
    if player = GameNetworkManager.Instance.localPlayerController then
        Some dissonance.LocalPlayerName
    else if isNull player.voicePlayerState then
        None
    else
        Some player.voicePlayerState.Name

/// <summary>
/// Create a default instance of a recording manager.
/// </summary>
let defaultRecordingManager (dissonance: DissonanceComms) =
    {   playerManager = defaultPlayerManager <| getVoiceId dissonance
        dissonance = dissonance
    }

/// <summary>
/// Get the directory where recordings are stored for the given player.
/// </summary>
let private getRecordingDirectory voiceId = $"{RecordingDirectory}/{voiceId}"

/// <summary>
/// Create a random file name for a recording.<br />
/// This returns <b>None</b> if the player isn't being recorded.
/// </summary>
let createRecordingName (manager: RecordingManager) (voiceId: string) : option<string> =
    monad' {
        let! _ = getPlayer manager.playerManager voiceId
        $"{getRecordingDirectory voiceId}/{DateTime.UtcNow.ToFileTime()}.wav"
    }

/// <summary>
/// Get a list of all the player recording's file names.
/// </summary>
let private getRecordings voiceId =
    let directory = $"{getRecordingDirectory voiceId}"
    try
        Directory.GetFiles directory
    with | error ->
        logError $"Failed to load directory: {directory}"
        logError $"{error}"
        zero

/// <summary>
/// Get a random recording for the given player.
/// </summary>
let getRandomRecording (dissonance: DissonanceComms) (random: Random) (player: PlayerControllerB) =
    let voiceId = getVoiceId dissonance player
    let recordings = getRecordings voiceId
    if recordings.Length = 0 then None
    else Some recordings[random.Next recordings.Length]

/// <summary>
/// Delete the directory containing recordings, ignoring the <b>IOException</b> if it gets thrown.
/// </summary>
let deleteRecordings () =
    try
        Directory.Delete($"{RecordingDirectory}", true)
    with
        | :? IOException as _ -> ()
        | error -> raise error

/// <summary>
/// Start recording a player.
/// </summary>
let startRecording (manager: RecordingManager) (player: PlayerControllerB) =
    { manager with playerManager = addPlayer manager.playerManager player }

/// <summary>
/// Stop the player from being recorded.
/// </summary>
let stopRecording (manager: RecordingManager) (player: PlayerControllerB) =
    { manager with playerManager = removePlayer manager.playerManager player }