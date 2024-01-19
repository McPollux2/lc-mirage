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
open System
open Mirage.core.Recording
open Mirage.Core.File
open Mirage.Core.Logger
open Mirage.Unity.PlayerManager
open Mirage.Core.Getter

type RecordAudio() =
    static let mutable isHost = false

    static let PlayerManager = ref None
    static let getPlayerManager = getter "RecordAudio" PlayerManager "PlayerManager"

    // TODO: Filter out only alive players.

    [<HarmonyPrefix>]
    [<HarmonyPatch(typeof<StartOfRound>, "Awake")>]
    static member ``delete all previous audio recordings``(__instance: StartOfRound) =
        isHost <- __instance.IsHost
        if isHost then 
            deleteRecordings()
            PlayerManager.Value <- Some <| defaultPlayerManager __instance _.voicePlayerState.Name

    [<HarmonyPrefix>]
    [<HarmonyPatch(typeof<PlayerControllerB>, "KillPlayerServerRpc")>]
    static member ``disable recording dead players``(__instance: PlayerControllerB) =
        handleResult <| monad' {
            if isHost then
                let! playerManager = getPlayerManager "``disable recording dead players``"
                PlayerManager.Value <- Some <| disablePlayer playerManager __instance.voicePlayerState.Name
        }

    [<HarmonyPrefix>]
    [<HarmonyPatch(typeof<BufferedDecoder>, "Prepare")>]
    static member ``enable audio playback recording``(__instance: BufferedDecoder, context: SessionContext) : bool =
        handleResult <| monad' {
            if isHost then
                let! playerManager = getPlayerManager "``enable audio playback recording``"
                if isPlayerActive playerManager context.PlayerName then
                    let filePath = $"{getRecordingsPath context.PlayerName}/{DateTime.UtcNow.ToFileTime()}"
                    __instance._diagnosticOutput <- new AudioFileWriter(filePath, __instance._waveFormat)
        }
        // This disables the original dissonance diagnostics recording functionality.
        // TODO: Keep the original dissonance behaviour while still recording to a custom location.
        false