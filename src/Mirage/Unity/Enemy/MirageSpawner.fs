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
module Mirage.Unity.Enemy.MirageSpawner

open Unity.Netcode
open FSharpPlus
open UnityEngine
open Mirage.Core.Logger
open Mirage.Core.Field
open Mirage.Unity.Network

[<Struct>]
type SpawnParams =
    {   mutable clientId: uint64
        mutable suitId: int
        mutable isEnemyOutside: bool
        mutable maskType: int
    }
    interface INetworkSerializable with
        member this.NetworkSerialize(serializer: BufferSerializer<'T>) : unit =
            serializer.SerializeValue(&this.clientId)
            serializer.SerializeValue(&this.suitId)
            serializer.SerializeValue(&this.isEnemyOutside)
            serializer.SerializeValue(&this.maskType)

/// <summary>
/// Create spawn params using the given mask item.
/// </summary>
let createSpawnParams (maskItem: HauntedMaskItem) =
    let clientId = maskItem.previousPlayerHeldBy.actualClientId
    let suitId = maskItem.previousPlayerHeldBy.currentSuitID
    let isEnemyOutside = not maskItem.previousPlayerHeldBy.isInsideFactory
    let maskType = maskItem.maskTypeId
    {   clientId = clientId
        suitId = suitId
        isEnemyOutside = isEnemyOutside
        maskType = maskType
    }

/// <summary>
/// Spawns a mirage, synchronizing the initial state with all clients.
/// <b>HauntedMaskItem</b> by itself was causing null reference exceptions.
/// </summary>
type MirageSpawner() =
    inherit NetworkBehaviour()

    let MaskItem = ref None
    let getMaskItem = getter "MirageSpawner" MaskItem "MaskItem"

    /// <summary>
    /// Spawn a mirage enemy locally. This does not affect any other clients.
    /// </summary>
    let spawnMirageLocal (mirageReference: NetworkObjectReference) (spawnParams: SpawnParams) =
        let mutable mirageObject = null
        if not <| mirageReference.TryGet &mirageObject then
            logError "MirageSpawner#SpawnMirage failed due to null network object."
        else
            let playerId = StartOfRound.Instance.ClientPlayerList[spawnParams.clientId]
            let player = StartOfRound.Instance.allPlayerScripts[playerId]
            let mirage = mirageObject.GetComponent<MaskedPlayerEnemy>()
            mirage.mimickingPlayer <- player
            mirage.SetSuit player.currentSuitID
            mirage.SetEnemyOutside spawnParams.isEnemyOutside
            mirage.SetMaskType spawnParams.maskType
            mirage.SetVisibilityOfMaskedEnemy()
            player.redirectToEnemy <- mirage

    member _.SetMaskItem(maskItem: HauntedMaskItem) =
        MaskItem.Value <- Option.ofObj maskItem

    member _.Awake () = logInfo "MirageSpawner#Awake is called"

    /// <summary>
    /// Spawn a mirage on all clients. This can only be invoked by the host.
    /// </summary>
    member this.SpawnMirage() =
        handleResult <| monad' {
            if not this.IsHost then logError "MirageSpawner#SpawnMirage can only be invoked by the host."
            else
                let! maskItem = getMaskItem "SpawnMirage"
                let spawnParams = createSpawnParams maskItem
                let playerId = StartOfRound.Instance.ClientPlayerList[spawnParams.clientId]
                let player = StartOfRound.Instance.allPlayerScripts[playerId]
                let mirageReference =
                    RoundManager.Instance.SpawnEnemyGameObject(
                        maskItem.previousPlayerHeldBy.transform.position,
                        player.transform.eulerAngles.y,
                        -1,
                        maskItem.mimicEnemy
                    )
                spawnMirageLocal mirageReference spawnParams
                this.SpawnMirageClientRpc(mirageReference, spawnParams)
        }

    [<ClientRpc>]
    member this.SpawnMirageClientRpc(mirageReference: NetworkObjectReference, spawnParams: SpawnParams) =
        if not this.IsHost then
            spawnMirageLocal mirageReference spawnParams
            //this.FinishSpawnServerRpc <| new ServerRpcParams()

    //[<ServerRpc(RequireOwnership = false)>]
    //member this.FinishSpawnServerRpc(serverParams: ServerRpcParams) =
    //    handleResult <| monad' {
    //        if this.IsHost && isValidClient this serverParams then
    //            let! maskItem = getMaskItem "FinishSpawnServerRpc"
    //            //maskItem.NetworkObject.Despawn()
    //    }