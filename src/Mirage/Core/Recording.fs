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
module Mirage.core.Recording

open FSharpPlus
open System
open System.IO
open GameNetcodeStuff
open Mirage.Core.File
open Mirage.Core.Logger

/// <summary>
/// Delete the audio directory, ignoring the <b>IOException</b> if it gets thrown.
/// </summary>
let deleteRecordings () =
    try
        Directory.Delete(RootDirectory + AudioDirectory, true)
    with
        | :? IOException as _ -> ()
        | error -> raise error

/// <summary>
/// Get the file paths of all recordings for the given player.
/// </summary>
let getRecordings (player: PlayerControllerB) : array<string> =
    let directory = getRecordingsPath player.voicePlayerState.Name
    try
        Directory.GetFiles directory
    with | error ->
        logError $"Failed to load directory: {directory}"
        logError $"{error}"
        zero

/// <summary>
/// Get the file path of a random recording for the given player (returns <b>None</b> if no recordings exist.
/// </summary>
let getRandomRecording (random: Random) (player: PlayerControllerB) : option<string> =
    let recordings = getRecordings player
    if recordings.Length = 0 then None
    else Some << Array.get recordings <| random.Next recordings.Length