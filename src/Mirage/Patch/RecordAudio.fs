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
open FSharpPlus
open Dissonance
open Dissonance.Audio.Capture
open Mirage.Core.Field
open Mirage.Core.Audio.Recording

let private get<'A> (field: Field<'A>) = field.Value

type RecordAudio() =
    /// <summary>
    /// The local player's recording file to write to.
    /// </summary>
    static let Recording = field()

    static let Dissonance = field()

    static let mutable roundStarted = false

    [<HarmonyPostfix>]
    [<HarmonyPatch(typeof<DissonanceComms>, "Start")>]
    static member ``store dissonance for later use``(__instance: DissonanceComms) = set Dissonance __instance

    [<HarmonyPrefix>]
    [<HarmonyPatch(typeof<StartOfRound>, "Awake")>]
    static member ``stop recording when a new round starts``() =
        roundStarted <- false

    [<HarmonyPostfix>]
    [<HarmonyPatch(typeof<StartOfRound>, "ResetPlayersLoadedValueClientRpc")>]
    static member ``deleting recordings when game starts``() =
        try deleteRecordings()
        with | _ -> ()
        roundStarted <- true

    [<HarmonyPrefix>]
    [<HarmonyPatch(typeof<BasePreprocessingPipeline>, "SendSamplesToSubscribers")>]
    static member ``record local player's audio if push-to-talk button is pushed``(__instance: BasePreprocessingPipeline, buffer: array<float32>) =
        ignore <| monad' {
            let! dissonance = get Dissonance
            if roundStarted && isRecording dissonance then
                let defaultRecording () =
                    let recording = createRecording __instance.OutputFormat
                    set Recording recording
                    recording
                let recording = Option.defaultWith defaultRecording <| get Recording
                writeRecording recording buffer
            else
                iter disposeRecording Recording.Value
                setNone Recording
        }

    [<HarmonyPrefix>]
    [<HarmonyPatch(typeof<BasePreprocessingPipeline>, "Dispose")>]
    static member ``dispose recording``() =
        iter disposeRecording Recording.Value
        setNone Recording