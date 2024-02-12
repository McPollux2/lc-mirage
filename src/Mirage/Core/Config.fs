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
module Mirage.Core.Config

open BepInEx.Configuration
open FSharpPlus
open System
open System.IO
open System.Runtime.Serialization.Formatters.Binary
open Unity.Collections
open Unity.Netcode
open Mirage.PluginInfo
open Mirage.Core.Field
open Mirage.Core.Logger

/// <summary>
/// Local preferences managed by BepInEx.
/// </summary>
type private LocalConfig(config: ConfigFile) =
    let [<Literal>] imitateSection = "Imitate player"
    let [<Literal>] maskedSection = "MaskedPlayerEnemy"
    member val ImitateMinDelay =
        config.Bind<int>(
            imitateSection,
            "MinimumDelay",
            7000,
            "The minimum amount of time in between voice playbacks (in milliseconds).\nThis only applies for masked enemies."
        )
    member val ImitateMaxDelay =
        config.Bind<int>(
            imitateSection,
            "MaximumDelay",
            12000,
            "The maximum amount of time in between voice playbacks (in milliseconds).\nThis only applies for masked enemies."
        )
    member val ImitateMinDelayNonMasked =
        config.Bind<int>(
            imitateSection,
            "MinimumDelayNonMasked",
            20000,
            "The minimum amount of time in between voice playbacks (in milliseconds).\nThis only applies for non-masked enemies."
        )
    member val ImitateMaxDelayNonMasked =
        config.Bind<int>(
            imitateSection,
            "MaximumDelayNonMasked",
            40000,
            "The maximum amount of time in between voice playbacks (in milliseconds).\nThis only applies for non-masked enemies."
        )
    member val DeleteRecordingsPerRound =
        config.Bind<bool>(
            imitateSection,
            "DeleteRecordingsPerRound",
            true,
            "Set to true to have recordings deleted in between rounds. Set to false to delete in between games."
        )
    member val MuteLocalPlayerVoice =
        config.Bind<bool>(
            imitateSection,
            "MuteLocalPlayerVoice",
            false,
            "Whether or not mimicking voices should be muted if it's the local player's voice."
        )
    member val EnableMaskedEnemy =
        config.Bind<bool>(
            imitateSection,
            "EnableMaskedEnemy",
            true,
            "Whether or not the masked enemy should mimic voices."
        )
    member val EnableBaboonHawk =
        config.Bind<bool>(
            imitateSection,
            "EnableBaboonHawk",
            false,
            "Whether or not the baboon hawk should mimic voices."
        )
    member val EnableBracken =
        config.Bind<bool>(
            imitateSection,
            "EnableBracken",
            false,
            "Whether or not the bracken should mimic voices."
        )
    member val EnableSpider =
        config.Bind<bool>(
            imitateSection,
            "EnableSpider",
            false,
            "Whether or not the spider should mimic voices."
        )
    member val EnableBees =
        config.Bind<bool>(
            imitateSection,
            "EnableBees",
            false,
            "Whether or not bees should mimic voices."
        )
    member val EnableLocustSwarm =
        config.Bind<bool>(
            imitateSection,
            "EnableLocustSwarm",
            false,
            "Whether or not locust swarms should mimic voices."
        )
    member val EnableCoilHead =
        config.Bind<bool>(
            imitateSection,
            "EnableCoilHead",
            false,
            "Whether or not the coil-head should mimic voices."
        )
    member val EnableEarthLeviathan =
        config.Bind<bool>(
            imitateSection,
            "EnableEarthLeviathan",
            false,
            "Whether or not the earth leviathan should mimic voices."
        )
    member val EnableEyelessDog =
        config.Bind<bool>(
            imitateSection,
            "EnableEyelessDog",
            false,
            "Whether or not the eyeless dog should mimic voices."
        )
    member val EnableForestKeeper =
        config.Bind<bool>(
            imitateSection,
            "EnableForestKeeper",
            false,
            "Whether or not the forest keeper should mimic voices."
        )
    member val EnableGhostGirl =
        config.Bind<bool>(
            imitateSection,
            "EnableGhostgirl",
            false,
            "Whether or not the ghost girl should mimic voices."
        )
    member val EnableHoardingBug =
        config.Bind<bool>(
            imitateSection,
            "EnableHoardingBug",
            false,
            "Whether or not the hoarding bug should mimic voices."
        )
    member val EnableHygrodere =
        config.Bind<bool>(
            imitateSection,
            "EnableHygrodere",
            false,
            "Whether or not the hygrodere should mimic voices."
        )
    member val EnableJester =
        config.Bind<bool>(
            imitateSection,
            "EnableJester",
            false,
            "Whether or not the jester should mimic voices."
        )
    member val EnableManticoil =
        config.Bind<bool>(
            imitateSection,
            "EnableManticoil",
            false,
            "Whether or not the manticoil should mimic voices."
        )
    member val EnableNutcracker =
        config.Bind<bool>(
            imitateSection,
            "EnableNutcracker",
            false,
            "Whether or not the nutcracker should mimic voices."
        )
    member val EnableSnareFlea =
        config.Bind<bool>(
            imitateSection,
            "EnableSnareFlea",
            false,
            "Whether or not the snare flea should mimic voices."
        )
    member val EnableSporeLizard =
        config.Bind<bool>(
            imitateSection,
            "EnableSporeLizard",
            false,
            "Whether or not the spore lizard should mimic voices."
        )
    member val EnableThumper =
        config.Bind<bool>(
            imitateSection,
            "EnableThumper",
            false,
            "Whether or not the thumper should mimic voices."
        )
    member val EnableModdedEnemies =
        config.Bind<bool>(
            imitateSection,
            "EnableModdedEnemies",
            false,
            "Whether or not all modded enemies should mimic voices."
        )
    member val EnablePenalty =
        config.Bind<bool>(
            "Credits",
            "EnablePenalty",
            false,
            "Whether the credits penalty should be applied during the end of a round."
        )
    member val EnableNaturalSpawn =
        config.Bind<bool>(
            maskedSection,
            "EnableNaturalSpawn",
            false,
            "Whether or not masked enemies should naturally spawn. Set this to false if you only want to spawn on player death. Set this to true if you want the vanilla spawning behaviour."
        )
    member val SpawnOnPlayerDeath =
        config.Bind<int>(
            maskedSection,
            "SpawnOnPlayerDeath",
            100,
            "The percent chance of a masked enemy spawning on player death (like a zombie). Must have a value of 0-100."
        )
    member val SpawnOnlyWhenPlayerAlone =
        config.Bind<bool>(
            maskedSection,
            "SpawnOnlyWhenPlayerAlone",
            false,
            "If set to true, SpawnOnPlayerDeath will only succeed if the dying player is alone."
        )
    member val EnableMask =
        config.Bind<bool>(
            maskedSection,
            "EnableMask",
            false,
            "Whether or not a masked enemy should have its mask texture"
        )
    member val EnableArmsOut =
        config.Bind<bool>(
            maskedSection,
            "EnableArmsOut",
            false,
            "Whether or not the arms out animation should be used."
        )

/// <summary>
/// Network synchronized configuration values. This is taken from the wiki:
/// https://lethal.wiki/dev/intermediate/custom-config-syncing
/// </summary>
[<Serializable>]
type SyncedConfig =
    {   imitateMinDelay: int
        imitateMaxDelay: int
        imitateMinDelayNonMasked: int
        imitateMaxDelayNonMasked: int
        muteLocalPlayerVoice: bool
        deleteRecordingsPerRound: bool
        enableMaskedEnemy: bool
        enableBaboonHawk: bool
        enableBracken: bool
        enableSpider: bool
        enableBees: bool
        enableLocustSwarm: bool
        enableCoilHead: bool
        enableEarthLeviathan: bool
        enableEyelessDog: bool
        enableForestKeeper: bool
        enableGhostGirl: bool
        enableHoardingBug: bool
        enableHygrodere: bool
        enableJester: bool
        enableManticoil: bool
        enableNutcracker: bool
        enableSnareFlea: bool
        enableSporeLizard: bool
        enableThumper: bool
        enableModdedEnemies: bool
        enablePenalty: bool
        enableNaturalSpawn: bool
        spawnOnPlayerDeath: int
        spawnOnlyWhenPlayerAlone: bool
        enableMask: bool
        enableArmsOut: bool
    }

let private toSyncedConfig (config: LocalConfig) =
    {   imitateMinDelay = config.ImitateMinDelay.Value
        imitateMaxDelay = config.ImitateMaxDelay.Value
        imitateMinDelayNonMasked = config.ImitateMinDelayNonMasked.Value
        imitateMaxDelayNonMasked = config.ImitateMaxDelayNonMasked.Value
        muteLocalPlayerVoice = config.MuteLocalPlayerVoice.Value
        deleteRecordingsPerRound = config.DeleteRecordingsPerRound.Value
        enableMaskedEnemy = config.EnableMaskedEnemy.Value
        enableBaboonHawk = config.EnableBaboonHawk.Value
        enableBracken = config.EnableBracken.Value
        enableSpider = config.EnableSpider.Value
        enableBees = config.EnableBees.Value
        enableLocustSwarm = config.EnableLocustSwarm.Value
        enableCoilHead = config.EnableCoilHead.Value
        enableEarthLeviathan = config.EnableEarthLeviathan.Value
        enableEyelessDog = config.EnableEyelessDog.Value
        enableForestKeeper = config.EnableForestKeeper.Value
        enableGhostGirl = config.EnableGhostGirl.Value
        enableHoardingBug = config.EnableHoardingBug.Value
        enableHygrodere = config.EnableHygrodere.Value
        enableJester = config.EnableJester.Value
        enableManticoil = config.EnableManticoil.Value
        enableNutcracker = config.EnableNutcracker.Value
        enableSnareFlea = config.EnableSnareFlea.Value
        enableSporeLizard = config.EnableSporeLizard.Value
        enableThumper = config.EnableThumper.Value
        enableModdedEnemies = config.EnableModdedEnemies.Value
        enablePenalty = config.EnablePenalty.Value
        enableNaturalSpawn = config.EnableNaturalSpawn.Value
        spawnOnPlayerDeath = config.SpawnOnPlayerDeath.Value
        spawnOnlyWhenPlayerAlone = config.SpawnOnlyWhenPlayerAlone.Value
        enableMask = config.EnableMask.Value
        enableArmsOut = config.EnableArmsOut.Value
    }

/// <summary>
/// An action for synchronizing the <b>SyncedConfig</b>.
/// </summary>
type SyncAction = RequestSync | ReceiveSync

/// <summary>
/// Convert the action to the message event name.
/// </summary>
let private toNamedMessage (action: SyncAction) =
    match action with
        | RequestSync -> $"{pluginId}_OnRequestConfigSync"
        | ReceiveSync -> $"{pluginId}_OnReceiveConfigSync"

let private messageManager () = NetworkManager.Singleton.CustomMessagingManager
let private isClient () = NetworkManager.Singleton.IsClient
let private isHost () = NetworkManager.Singleton.IsHost

let private LocalConfig = field()
let private SyncedConfig = field()

/// <summary>
/// Retrieves a <b>SyncedConfig</b>, either from being synced with the host, or taken by the local config.<br />
/// This requires <b>initConfig</b> to be invoked to work.
/// </summary>
let getConfig () =
    let errorIfMissing () =
        invalidOp "Failed to retrieve local config. This is probably due to running initConfig."
    match getValue SyncedConfig with
        | Some config -> config
        | None -> Option.defaultWith errorIfMissing (toSyncedConfig <!> getValue LocalConfig)

/// <summary>
/// Initialize the configuration. Does nothing if you run it more than once.
/// </summary>
let initConfig (file: ConfigFile) =
    monad' {
        if Option.isNone LocalConfig.Value then
            let config = new LocalConfig(file)
            let errorHeader = "Configuration is invalid. "
            let minDelayKey = config.ImitateMinDelay.Definition.Key
            let maxDelayKey = config.ImitateMaxDelay.Definition.Key
            let spawnOnPlayerDeathKey = config.SpawnOnPlayerDeath.Definition.Key
            if config.ImitateMinDelay.Value < 0 then
                return! Error $"{errorHeader}{minDelayKey} cannot have a value smaller than 0."
            if config.ImitateMaxDelay.Value < 0 then
                return! Error $"{errorHeader}{maxDelayKey} cannot have a value smaller than 0."
            if config.ImitateMinDelay.Value > config.ImitateMaxDelay.Value then
                return! Error $"{errorHeader}{minDelayKey} must have a value smaller than {maxDelayKey}"
            if config.SpawnOnPlayerDeath.Value < 0 || config.SpawnOnPlayerDeath.Value > 100 then
                return! Error $"{errorHeader}{spawnOnPlayerDeathKey} must have a value between 0-100."
            set LocalConfig config
    }

let private serializeToBytes<'A> (value: 'A) : array<byte> =
    let formatter = new BinaryFormatter()
    use stream = new MemoryStream()
    try
        formatter.Serialize(stream, value)
        stream.ToArray()
    with | error ->
        logError $"Failed to serialize value: {error}"
        null

let private deserializeFromBytes<'A> (data: array<byte>) : 'A =
    let formatter = new BinaryFormatter()
    use stream = new MemoryStream(data)
    try
        formatter.Deserialize stream :?> 'A
    with | error ->
        logError $"Failed to deserialize bytes: {error}"
        Unchecked.defaultof<'A>

/// <summary>
/// Revert the synchronized config and use the default values.
/// </summary>
let revertSync () = setNone SyncedConfig

/// <summary>
/// Request to synchronize the local config with the host.
/// </summary>
let requestSync () =
    if isClient() then
        use stream = new FastBufferWriter(sizeof<int32>, Allocator.Temp) 
        messageManager().SendNamedMessage(toNamedMessage RequestSync, 0UL, stream)

let private onRequestSync (clientId: uint64) _ =
    if isHost() then
        let bytes = serializeToBytes <| getConfig()
        let bytesLength = bytes.Length
        use writer = new FastBufferWriter(bytesLength + sizeof<int32>, Allocator.Temp)
        try
            writer.WriteValueSafe &bytesLength
            writer.WriteBytesSafe bytes
            messageManager().SendNamedMessage(toNamedMessage ReceiveSync, clientId, writer)
        with | error ->
            logError $"Failed during onRequestSync: {error}"

let private onReceiveSync _ (reader: FastBufferReader) =
    if not <| isHost() then
        handleResult <| monad' {
            if not <| reader.TryBeginRead sizeof<int> then
                return! Error "onReceiveSync failed while reading beginning of buffer."
            let mutable bytesLength = 0
            reader.ReadValueSafe &bytesLength
            if not <| reader.TryBeginRead(bytesLength) then
                return! Error "onReceiveSync failed. Host could not synchronize config."
            let bytes = Array.zeroCreate<byte> bytesLength
            reader.ReadBytesSafe(ref bytes, bytesLength)
            set SyncedConfig <| deserializeFromBytes bytes
        }

/// <summary>
/// Register the named message handler for the given action.
/// </summary>
let registerHandler (action: SyncAction) =
    let message = toNamedMessage action
    let register handler = messageManager().RegisterNamedMessageHandler(message, handler)
    match action with
        | RequestSync -> register onRequestSync
        | ReceiveSync -> register onReceiveSync