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
open Mirage.Core.Logger
open System.Collections.Generic
open UnityEngine
open System.Reflection.Emit

let private get<'A> (field: Field<'A>) = field.Value

type RecordAudio() =
    /// <summary>
    /// The host's recording file to write to.
    /// This is set to <b>None</b> whenever the host is muted.
    /// </summary>
    static let HostRecording : Field<AudioFileWriter> = ref None

    static let RecordingManager = ref None
    static let getLocalPlayer () = GameNetworkManager.Instance.localPlayerController

    /// <summary>
    /// Recording files received from clients. The table value contains the voice id and audio file to write to.
    /// </summary>
    static let clientRecordings = ConditionalWeakTable<BufferedDecoder, string * Option<AudioFileWriter>>()

    [<HarmonyPostfix>]
    [<HarmonyPatch(typeof<DissonanceComms>, "Start")>]
    static member ``initialize recording manager``(__instance: DissonanceComms) =
        if GameNetworkManager.Instance.isHostingGame then
            logInfo "hosting game. creating recording manager"
            let recordingManager = defaultRecordingManager __instance
            set RecordingManager recordingManager
            let localPlayer = getLocalPlayer()
            //logInfo $"local player: {getVoiceId dissonance localPlayer}"
            set RecordingManager <| flip startRecording localPlayer recordingManager
            logInfo "finished creating recording manager"
            //defaultRecordingManager dissonance
            //    |> flip startRecording (getLocalPlayer())
            //    |> set RecordingManager

    [<HarmonyPostfix>]
    [<HarmonyPatch(typeof<StartOfRound>, "StartGame")>]
    static member ``initialize recording manager with host player added to recording manager``(__instance: StartOfRound) =
        if __instance.IsHost then
            // TODO: dispose all audio file writers.
            clientRecordings.Clear()

            logInfo $"game started. deleting recordings"
            deleteRecordings()

    [<HarmonyPatch(typeof<StartOfRound>, "RefreshPlayerVoicePlaybackObjects")>]
    static member Transpiler (instructions: IEnumerable<CodeInstruction>) =
        // Call the method specified in the code instruction, at the end of RefreshPlayerVoicePlaybackObjects.
        let targetMethod = AccessTools.Method(typeof<AudioSource>, "set_outputAudioMixerGroup")
        seq {
            for instruction in instructions do
                if instruction.Calls targetMethod then
                    yield new CodeInstruction(OpCodes.Ldloc_1)
                    yield new CodeInstruction(
                        OpCodes.Call,
                        AccessTools.Method(
                            typeof<RecordAudio>, "add non-host player to recording manager",
                            [|typeof<PlayerControllerB>|]
                        )
                    )
                yield instruction
        }

    // This does not have patch annotations because it's called by the above transpiler.
    static member ``add non-host player to recording manager``(player: PlayerControllerB) =
        if not player.isPlayerDead then
            logInfo $"adding non-host player: {player.voicePlayerState.Name}"
            ignore <| monad' {
                //get RecordingManager
                //    |>> flip startRecording player
                //    |> setOption RecordingManager
                let! recordingManager = get RecordingManager
                logInfo "after getting recording manager (add non-host to recording manager)"
                set RecordingManager <| startRecording recordingManager player
                logInfo $"finished adding non-host player: {player.voicePlayerState.Name}"
            }

    [<HarmonyPrefix>]
    [<HarmonyPatch(typeof<PlayerControllerB>, "KillPlayerServerRpc")>]
    static member ``stop recording player on death``(__instance: PlayerControllerB) =
        if isNull __instance.voicePlayerState then
            logInfo "stopping record for player (probably host since voiceplayerstate is null)"
        else
            logInfo $"stopping recording for player: {__instance.voicePlayerState.Name}"
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
                logInfo "got recording. disposing."
                setNone HostRecording
                recording.Dispose()
                logInfo "done stopping host audio"
            else
                let! recording =
                    monad' {
                        match get HostRecording with
                            | Some recording ->
                                logInfo "got previous recording. returning"
                                recording
                            |  None ->
                                logInfo "getting voice id"
                                let! voiceId = getVoiceId recordingManager.dissonance <| getLocalPlayer()
                                logInfo "getting file name"
                                let! fileName = createRecordingName recordingManager voiceId
                                logInfo $"got file name: {fileName}"
                                let recording = new AudioFileWriter(fileName, __instance.OutputFormat)
                                logInfo $"creating recording."
                                set HostRecording recording
                                logInfo $"set recording. finished"
                                recording
                    }
                logInfo "got recording. writing smaples."
                recording.WriteSamples(new ArraySegment<float32>(buffer))
                logInfo "done recording host"
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
        logInfo $"new player voiceid added: {context.PlayerName}"
        clientRecordings.Add(__instance, (context.PlayerName, None))

    [<HarmonyPrefix>]
    [<HarmonyPatch(typeof<BufferedDecoder>, "Read")>]
    static member ``record non-host player's audio``(__instance: BufferedDecoder, frame: ArraySegment<float32>) =
        ignore <| monad' {
            let! recordingManager = get RecordingManager
            let! recording =
                monad' {
                    let mutable value = zero
                    ignore <| clientRecordings.TryGetValue(__instance, &value)
                    match value with
                        | voiceId, None -> 
                            logInfo $"creating new recording non-host. voiceId: {voiceId}"
                            let! fileName = createRecordingName recordingManager voiceId
                            logInfo $"created file: {fileName}"
                            let recording = new AudioFileWriter(fileName, __instance.WaveFormat)
                            clientRecordings.AddOrUpdate(__instance, (voiceId, Some recording))
                            recording
                        | _, Some recording ->
                            logInfo "recording already exists. returning.."
                            recording
                }
            (recording: AudioFileWriter).WriteSamples frame
        }

    [<HarmonyPrefix>]
    [<HarmonyPatch(typeof<BufferedDecoder>, "Reset")>]
    static member ``dispose client recording when finished writing``(__instance: BufferedDecoder) =
        let mutable value = zero
        if clientRecordings.TryGetValue(__instance, &value) then
            ignore << map dispose <| snd value
            ignore <| clientRecordings.Remove __instance