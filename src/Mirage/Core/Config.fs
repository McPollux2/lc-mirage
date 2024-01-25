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
    member val FooBar = config.Bind<string>("Test", "FooBar", "foo bar baz", "test test test")

/// <summary>
/// Network synchronized configuration values. This is taken from the wiki:
/// https://lethal.wiki/dev/intermediate/custom-config-syncing
/// </summary>
[<Serializable>]
type SyncedConfig = { fooBar: string }

let private toSyncedConfig (config: LocalConfig) =
    {   fooBar = config.FooBar.Value
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

let private get<'A> = getter<'A> "Config"

/// <summary>
/// Get the <b>SyncedConfig</b> if it's initialized. Otherwise return the <b>LocalConfig</b> (converted to SyncedConfig).
/// </summary>
let getConfig methodName =
    let getSynced = get SyncedConfig "SyncedConfig"
    let getLocal = get LocalConfig "LocalConfig"
    match getSynced methodName with
        | Ok config -> Ok config
        | Error _ -> toSyncedConfig <!> getLocal methodName

/// <summary>
/// Same as <b>getConfig</b>, but the error message is ignored.
/// </summary>
let getConfig' () = Result.toOption <| getConfig zero

/// <summary>
/// Initialize the configuration.
/// </summary>
let initConfig (config: ConfigFile) =
    if Option.isNone LocalConfig.Value then
        set LocalConfig <| new LocalConfig(config)

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
        handleResult <| monad' {
            logInfo $"Config sync request received from client: {clientId}"
            let! config = getConfig "onRequestSync"
            let bytes = serializeToBytes config
            let bytesLength = bytes.Length
            use writer = new FastBufferWriter(bytesLength + sizeof<int32>, Allocator.Temp)
            try
                writer.WriteValueSafe &bytesLength
                writer.WriteBytesSafe bytes
                messageManager().SendNamedMessage(toNamedMessage ReceiveSync, clientId, writer)
            with | error ->
                logError $"Failed during onRequestSync: {error}"
        }

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
            logInfo "Successfully synced config with host."
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