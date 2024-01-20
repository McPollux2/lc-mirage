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

open System
open System.Runtime.CompilerServices
open HarmonyLib
open FSharpPlus
open FSharpPlus.Data
open GameNetcodeStuff
open Dissonance
open Dissonance.Audio
open Dissonance.Audio.Capture
open Dissonance.Audio.Playback
open Mirage.Core.Field
open Mirage.Unity.RecordingManager

let private get<'A> (field: Field<'A>) = Option.ofResult <| getter zero field zero zero

type RecordAudio() =
    /// <summary>
    /// The host's recording file to write to.
    /// This is set to <b>None</b> whenever the host is muted.
    /// </summary>
    static let HostRecording : Ref<Option<AudioFileWriter>>= ref None

    static let RecordingManager = ref None
    static let getLocalPlayer () = GameNetworkManager.Instance.localPlayerController

    /// <summary>
    /// Recording files received from clients.
    /// </summary>
    static let clientRecordings = ConditionalWeakTable<BufferedDecoder, AudioFileWriter>()

    [<HarmonyPostfix>]
    [<HarmonyPatch(typeof<StartOfRound>, "StartGame")>]
    static member ``initialize recording manager``(__instance: StartOfRound) =
        if __instance.IsHost then
            let dissonance = UnityEngine.Object.FindObjectOfType<DissonanceComms>()
            deleteRecordings()
            defaultRecordingManager dissonance
                |> flip startRecording (getLocalPlayer())
                |> set RecordingManager
            clientRecordings.Clear()

    [<HarmonyPostfix>]
    [<HarmonyPatch(typeof<StartOfRound>, "OnClientConnect")>]
    static member ``start recording non-host player on connect``(__instance: StartOfRound, clientId: uint64) =
        if __instance.IsHost then
            let playerId = StartOfRound.Instance.ClientPlayerList[clientId]
            let player = StartOfRound.Instance.allPlayerScripts[playerId]
            get RecordingManager
                |>> flip startRecording player
                |> setOption RecordingManager

    [<HarmonyPrefix>]
    [<HarmonyPatch(typeof<PlayerControllerB>, "KillPlayerServerRpc")>]
    static member ``stop recording player on death``(__instance: PlayerControllerB) =
        get RecordingManager
            |>> flip stopRecording __instance
            |> setOption RecordingManager

    [<HarmonyPrefix>]
    [<HarmonyPatch(typeof<BasePreprocessingPipeline>, "SendSamplesToSubscribers")>]
    static member ``record host player's audio if not muted``(__instance: BasePreprocessingPipeline, buffer: array<float32>) =
        ignore <| monad' {
            let! recordingManager = get RecordingManager
            if recordingManager.dissonance.IsMuted then
                let! recording = get HostRecording
                setNone HostRecording
                recording.Dispose()
            else
                let! recording =
                    monad' {
                        match get HostRecording with
                            | Some recording -> recording
                            |  None ->
                                let! voiceId = getVoiceId recordingManager.dissonance <| getLocalPlayer()
                                let! fileName = createRecordingName recordingManager voiceId
                                let recording = new AudioFileWriter(fileName, __instance.OutputFormat)
                                set HostRecording recording
                                recording
                    }
                recording.WriteSamples(new ArraySegment<float32>(buffer))
        }

    [<HarmonyPrefix>]
    [<HarmonyPatch(typeof<BasePreprocessingPipeline>, "Dispose")>]
    static member ``dispose host recording when finished writing``(__instance: BasePreprocessingPipeline) =
        ignore <| monad' {
            let! recording = get HostRecording
            setNone HostRecording
            recording.Dispose()
        }

    [<HarmonyPrefix>]
    [<HarmonyPatch(typeof<BufferedDecoder>, "Prepare")>]
    static member ``prepare to record for non-host players``(__instance: BufferedDecoder, context: SessionContext) =
        ignore <| monad' {
            let! recordingManager = get RecordingManager
            let! fileName = createRecordingName recordingManager context.PlayerName
            let recording = new AudioFileWriter(fileName, __instance._waveFormat)
            clientRecordings.Add(__instance, recording)
        }

    [<HarmonyPrefix>]
    [<HarmonyPatch(typeof<BufferedDecoder>, "Read")>]
    static member ``record non-host player's audio``(__instance: BufferedDecoder, frame: ArraySegment<float32>) =
        let mutable recording = null
        if clientRecordings.TryGetValue(__instance, &recording) then
            recording.WriteSamples(frame)

    [<HarmonyPrefix>]
    [<HarmonyPatch(typeof<BufferedDecoder>, "Reset")>]
    static member ``dispose client recording when finished writing``(__instance: BufferedDecoder) =
        let mutable recording = null
        if clientRecordings.TryGetValue(__instance, &recording) then
            recording.Dispose()