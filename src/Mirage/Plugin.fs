namespace Mirage

open BepInEx
open HarmonyLib
open Mirage.PluginInfo
open Netcode

[<BepInPlugin(pluginName, pluginId, pluginVersion)>]
type Plugin() =
    inherit BaseUnityPlugin()

    member _.Awake() =
        initNetcodePatcher()
        let harmony = new Harmony(pluginId)
        ()