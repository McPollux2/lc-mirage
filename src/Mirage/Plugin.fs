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
namespace Mirage

open UnityEngine
open System
open System.IO
open BepInEx
open FSharpPlus
open HarmonyLib
open Netcode
open NAudio.Lame
open Mirage.PluginInfo
open Mirage.Core.Config
open Mirage.Core.Logger
open Mirage.Core.Audio.Recording
open Mirage.Patch.NetworkPrefab
open Mirage.Patch.SyncConfig
open Mirage.Patch.RemovePenalty
open Mirage.Patch.RecordAudio
open Mirage.Patch.SpawnMaskedEnemy
open Mirage.Patch.IgnoreNavMesh

[<BepInPlugin(pluginName, pluginId, pluginVersion)>]
type Plugin() =
    inherit BaseUnityPlugin()

    let onError () = logError "Failed to initialize Mirage. Plugin is disabled."

    member this.Awake() =
        handleResultWith onError <| monad' {
            initNetcodePatcher()
            return! initConfig this.Config
            ignore <| LameDLL.LoadNativeDLL [|Path.GetDirectoryName this.Info.Location|]
            Application.add_quitting deleteRecordings
            let harmony = new Harmony(pluginId)
            iter (unbox<Type> >> harmony.PatchAll) 
                [   typeof<RegisterPrefab>
                    typeof<RecordAudio>
                    typeof<SpawnMaskedEnemy>
                    typeof<SyncConfig>
                    typeof<RemovePenalty>
                    typeof<IgnoreNavMesh>
                ]
        }