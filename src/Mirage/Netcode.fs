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