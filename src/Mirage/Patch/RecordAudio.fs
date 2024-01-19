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
open GameNetcodeStuff
open Dissonance
open Dissonance.Audio
open Dissonance.Audio.Capture
open Dissonance.Audio.Playback
open Mirage.Core.Field
open Mirage.Unity.RecordingManager
open Mirage.Core.Logger

type RecordAudio() =
    static let RecordingManager = ref None
    static let getRecordingManager = getter zero RecordingManager zero

    /// <summary>
    /// This is basically like adding an <b>AudioFileWriter</b> field to <b>BufferedDecoder</b>.
    /// </summary>
    static let decoderRecordings = ConditionalWeakTable<BufferedDecoder, AudioFileWriter>()

    [<HarmonyPostfix>]
    [<HarmonyPatch(typeof<StartOfRound>, "StartGame")>]
    static member ``initialize recording manager``(__instance: StartOfRound) =
        if __instance.IsHost then
            let dissonance = UnityEngine.Object.FindObjectOfType<DissonanceComms>()
            set RecordingManager <| defaultRecordingManager dissonance __instance
            decoderRecordings.Clear()

    [<HarmonyPrefix>]
    [<HarmonyPatch(typeof<PlayerControllerB>, "KillPlayerServerRpc")>]
    static member ``stop recording player on death``() =
        ignore <| monad' {
            let! recordingManager = getRecordingManager zero
            set RecordingManager << stopRecording recordingManager <| getLocalVoiceId recordingManager
        }

    [<HarmonyPrefix>]
    [<HarmonyPatch(typeof<BasePreprocessingPipeline>, "SendSamplesToSubscribers")>]
    static member ``record host player's audio``(__instance: BasePreprocessingPipeline, buffer: array<float32>) =
        ignore <| monad' {
            let! recordingManager = getRecordingManager zero
            let voiceId = getLocalVoiceId recordingManager
            if isRecordingPlayer recordingManager voiceId then
                let fileName = createRecordingName voiceId
                logInfo $"recording local player: {fileName}"
                let recorder = new AudioFileWriter(fileName, __instance.OutputFormat)
                recorder.WriteSamples(new ArraySegment<float32>(buffer))
        }

    [<HarmonyPrefix>]
    [<HarmonyPatch(typeof<BufferedDecoder>, "Prepare")>]
    static member ``prepare to record for non-host players``(__instance: BufferedDecoder) =
        logInfo "buffered decoder prepare"
        ignore <| monad' {
            let! recordingManager = getRecordingManager zero
            let voiceId = getLocalVoiceId recordingManager
            if isRecordingPlayer recordingManager voiceId then
                let fileName = createRecordingName voiceId
                let recorder = new AudioFileWriter(fileName, __instance._waveFormat)
                decoderRecordings.Add(__instance, recorder)
        }

    [<HarmonyPrefix>]
    [<HarmonyPatch(typeof<BufferedDecoder>, "Read")>]
    static member ``record non-host player's audio``(__instance: BufferedDecoder, frame: ArraySegment<float32>) =
        logInfo "buffered decoder read"
        let mutable recording = null
        if decoderRecordings.TryGetValue(__instance, &recording) then
            recording.WriteSamples(frame)