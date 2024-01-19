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
    private
        {   dissonance: DissonanceComms
            round: StartOfRound
            playerManager: PlayerManager<string>
            random: Random
        }

/// <summary>
/// Retrieve the player's voice id. By default, the local player's voice player state is null,
/// so we fetch it from dissonance comms instead.
/// </summary>
let private getVoiceId (dissonance: DissonanceComms) (player: PlayerControllerB) =
    if player.IsLocalPlayer then
        dissonance.LocalPlayerName
    else if isNull player.voicePlayerState then
        invalidOp "RecordingManager#getVoiceId failed, due to player.voicePlayerState being null."
    else
        player.voicePlayerState.Name

/// <summary>
/// Initialize the recording manager. This should be called at when all player scripts are ready.
/// </summary>
let defaultRecordingManager (dissonance: DissonanceComms) (round: StartOfRound) =
    {   dissonance = dissonance
        round = round
        playerManager = defaultPlayerManager round <| getVoiceId dissonance
        random = new Random()
    }

/// <summary>
/// Get the directory where recordings are stored for the given player.
/// </summary>
let private getRecordingDirectory voiceId = $"{RecordingDirectory}/{voiceId}"

/// <summary>
/// Create a random file name for a recording.
/// </summary>
let createRecordingName (voiceId: string) : string =
    $"{getRecordingDirectory voiceId}/{DateTime.UtcNow.ToFileTime()}.wav"

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
let getRandomRecording (manager: RecordingManager) (player: PlayerControllerB) =
    let voiceId = getVoiceId manager.dissonance player
    let recordings = getRecordings voiceId
    if recordings.Length = 0 then None
    else Some recordings[manager.random.Next recordings.Length]

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
/// Whether the player should be recorded or not.
/// </summary>
let isRecordingPlayer (manager: RecordingManager) : string -> bool =
    isPlayerActive manager.playerManager

/// <summary>
/// Stop the player from being recorded.
/// </summary>
let stopRecording (manager: RecordingManager) (voiceId: string) =
    { manager with playerManager = disablePlayer manager.playerManager voiceId }

/// <summary>
/// Get the local player's voice id.
/// </summary>
let getLocalVoiceId (manager: RecordingManager) = manager.dissonance.LocalPlayerName