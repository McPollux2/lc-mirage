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

open System
open BepInEx
open FSharpPlus
open HarmonyLib
open Netcode
open NAudio.Lame
open Mirage.PluginInfo
open Mirage.Core.File
open Mirage.Patch.RecordAudio
open Mirage.Patch.SpawnMirage
open Mirage.Patch.NetworkPrefab

[<BepInPlugin(pluginName, pluginId, pluginVersion)>]
type Plugin() =
    inherit BaseUnityPlugin()

    member _.Awake() =
        initNetcodePatcher()
        ignore <| LameDLL.LoadNativeDLL $"{RootDirectory}/BepInEx/plugins/"
        let harmony = new Harmony(pluginId)
        iter (unbox<Type> >> harmony.PatchAll) 
            [   typeof<RegisterPrefab>
                typeof<RecordAudio>
                typeof<SpawnMirage>
            ]
        harmony.Patch(
            original =
                AccessTools.Method(
                    AccessTools.Inner(typeof<MaskedPlayerEnemy>, "<killAnimation>d__102"), 
                    "MoveNext"
                ),
            transpiler = new HarmonyMethod(typeof<SpawnMirage>, "prevent duplicate mirage spawn")
        )