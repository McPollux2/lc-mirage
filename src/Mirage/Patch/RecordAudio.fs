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
module Mirage.Patch.RecordAudio

open System;
open System.IO
open HarmonyLib
open Dissonance.Audio.Playback
open Dissonance.Audio
open Module.Core.File
open Mirage.Core.Logger

type RecordAudio() =
    static let mutable isHost = false

    // TODO: Filter out only alive players.

    [<HarmonyPrefix>]
    [<HarmonyPatch(typeof<StartOfRound>, "Awake")>]
    static member ``delete all previous audio recordings``(__instance: StartOfRound) =
        isHost <- __instance.IsHost
        if isHost then
            try
                Directory.Delete(RootDirectory + AudioDirectory, true)
            with
                | :? IOException as _ -> ()
                | error -> raise error

    [<HarmonyPrefix>]
    [<HarmonyPatch(typeof<BufferedDecoder>, "Prepare")>]
    static member ``enable audio playback recording``(__instance: BufferedDecoder, context: SessionContext) =
        if isHost then
            logInfo "Replacing dissonance diagnostics with a custom destination."
            logInfo "If you have a mod that depends on it, this will cause issues with that mod."
            let filePath = $"{getPlayerRecordingsDirectory context.PlayerName}/{DateTime.UtcNow.ToFileTime()}"
            __instance._diagnosticOutput <- new AudioFileWriter(filePath, __instance._waveFormat)

        // TODO: Figure out how to make this patch save audio independently of whether
        // or not dissonance diagnostics is enabled (this currently replaces its functionality).
        false