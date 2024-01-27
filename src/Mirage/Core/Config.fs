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
    member val ImitateMinDelay =
        config.Bind<int>(
            imitateSection,
            "MinimumDelay",
            7000,
            "The minimum amount of time in between voice playbacks (in milliseconds)."
        )
    member val ImitateMaxDelay =
        config.Bind<int>(
            imitateSection,
            "MaximumDelay",
            12000,
            "The maximum amount of time in between voice playbacks (in milliseconds)."
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
            "MaskedPlayerEnemy",
            "EnableNaturalSpawn",
            false,
            "Whether or not masked enemies should naturally spawn. Enabling this can potentially cause issues."
        )

/// <summary>
/// Network synchronized configuration values. This is taken from the wiki:
/// https://lethal.wiki/dev/intermediate/custom-config-syncing
/// </summary>
[<Serializable>]
type SyncedConfig =
    {   imitateMinDelay: int
        imitateMaxDelay: int
        enablePenalty: bool
        enableNaturalSpawn: bool
    }

let private toSyncedConfig (config: LocalConfig) =
    {   imitateMinDelay = config.ImitateMinDelay.Value
        imitateMaxDelay = config.ImitateMaxDelay.Value
        enablePenalty = config.EnablePenalty.Value
        enableNaturalSpawn = config.EnableNaturalSpawn.Value
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
            if config.ImitateMinDelay.Value < 0 then
                return! Error $"{errorHeader}{minDelayKey} cannot have a value smaller than 0."
            if config.ImitateMaxDelay.Value < 0 then
                return! Error $"{errorHeader}{maxDelayKey} cannot have a value smaller than 0."
            if config.ImitateMinDelay.Value > config.ImitateMaxDelay.Value then
                return! Error $"{errorHeader}{minDelayKey} must have a value smaller than {maxDelayKey}"
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