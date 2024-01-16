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

open HarmonyLib
open Dissonance.Audio.Playback
open Dissonance.Audio
open FSharpPlus
open GameNetcodeStuff
open Mirage.Core.Logger
open Mirage.Core.Audio.Recording
open Mirage.Core.Getter

type RecordAudio() =
    static let mutable isHost = false

    static let RecordingManager = ref None
    static let getRecordingManager = getter "RecordAudio" RecordingManager "RecordingManager"

    // TODO: Filter out only alive players.

    [<HarmonyPrefix>]
    [<HarmonyPatch(typeof<StartOfRound>, "Awake")>]
    static member ``delete all previous audio recordings``(__instance: StartOfRound) =
        isHost <- __instance.IsHost
        if isHost then 
            deleteRecordings()
            RecordingManager.Value <- Some <| startRecordingManager __instance

    [<HarmonyPrefix>]
    [<HarmonyPatch(typeof<PlayerControllerB>, "KillPlayerServerRpc")>]
    static member ``disable recording dead players``(__instance: PlayerControllerB) =
        handleResult <| monad' {
            if isHost then
                let! recordingManager = getRecordingManager "``disable recording dead players``"
                RecordingManager.Value <- Some <| setPlayerDead recordingManager __instance
        }

    [<HarmonyPrefix>]
    [<HarmonyPatch(typeof<BufferedDecoder>, "Prepare")>]
    static member ``enable audio playback recording``(__instance: BufferedDecoder, context: SessionContext) : bool =
        handleResult <| monad' {
            if isHost then
                let! recordingManager = getRecordingManager "``enable audio playback recording``"
                if isPlayerAlive recordingManager context.PlayerName then
                    __instance._diagnosticOutput <- new AudioFileWriter(createRecordingPath context.PlayerName, __instance._waveFormat)
        }
        // This disables the original dissonance diagnostics recording functionality.
        // TODO: Keep the original dissonance behaviour while still recording to a custom location.
        false