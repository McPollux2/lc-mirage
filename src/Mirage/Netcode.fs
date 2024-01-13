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
module Mirage.Netcode

open System.Reflection
open UnityEngine
open FSharpPlus

let private flags =
    BindingFlags.NonPublic
        ||| BindingFlags.Instance
        ||| BindingFlags.Static

let private invokeMethod (method: MethodInfo) =
    let attributes = method.GetCustomAttributes(typeof<RuntimeInitializeOnLoadMethodAttribute>, false)
    if attributes.Length > 0 then
        ignore <| method.Invoke(null, null)

/// <summary>
/// This must be run once (and only once) on plugin startup for the netcode patcher to work.<br />
/// See: https://github.com/EvaisaDev/UnityNetcodePatcher/tree/c64eb86e74e85e1badc442adc0bf270bab0df6b6#preparing-mods-for-patching
/// </summary>
let initNetcodePatcher () =
    Assembly.GetExecutingAssembly().GetTypes()
        >>= _.GetMethods(flags)
        |> iter invokeMethod