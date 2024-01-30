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
module Mirage.Unity.MirageSpawner

open GameNetcodeStuff
open Unity.Netcode
open FSharpPlus
open Mirage.Core.Logger

[<Struct>]
type SpawnParams =
    {   mutable clientId: uint64
        mutable suitId: int
        mutable isEnemyOutside: bool
    }
    interface INetworkSerializable with
        member this.NetworkSerialize(serializer: BufferSerializer<'T>) : unit =
            serializer.SerializeValue(&this.clientId)
            serializer.SerializeValue(&this.suitId)
            serializer.SerializeValue(&this.isEnemyOutside)

/// <summary>
/// Create spawn params using the given player.
/// </summary>
let createSpawnParams (player: PlayerControllerB) =
    let clientId = player.actualClientId
    let suitId = player.currentSuitID
    let isEnemyOutside = not player.isInsideFactory
    {   clientId = clientId
        suitId = suitId
        isEnemyOutside = isEnemyOutside
    }

/// <summary>
/// Set the player to mimic the visuals/voice.
/// </summary>
let setMiragePlayer (mirage: MaskedPlayerEnemy) (player: PlayerControllerB) =
    mirage.mimickingPlayer <- player
    mirage.SetSuit player.currentSuitID
    mirage.SetEnemyOutside (not player.isInsideFactory)
    mirage.SetVisibilityOfMaskedEnemy()
    player.redirectToEnemy <- mirage

/// <summary>
/// Spawns a mirage, synchronizing the initial state with all clients.
/// <b>HauntedMaskItem</b> by itself was causing null reference exceptions.
/// </summary>
type MirageSpawner() =
    inherit NetworkBehaviour()

    let mutable spawned = false

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
            setMiragePlayer mirage player

    /// <summary>
    /// Whether a Mirage has been spawned or not.
    /// </summary>
    member _.IsSpawned() = spawned

    /// <summary>
    /// Spawn a mirage on all clients. This can only be invoked by the host.
    /// </summary>
    member this.SpawnMirage(mask: HauntedMaskItem) =
        handleResult <| monad' {
            if not this.IsHost then logError "MirageSpawner#SpawnMirage can only be invoked by the host."
            else
                spawned <- true
                let spawnParams = createSpawnParams mask.previousPlayerHeldBy
                let mirageReference =
                    RoundManager.Instance.SpawnEnemyGameObject(
                        mask.previousPlayerHeldBy.transform.position,
                        mask.previousPlayerHeldBy.transform.eulerAngles.y,
                        -1,
                        mask.mimicEnemy
                    )
                spawnMirageLocal mirageReference spawnParams
                this.SpawnMirageClientRpc(mirageReference, spawnParams)
        }

    [<ClientRpc>]
    member this.SpawnMirageClientRpc(mirageReference: NetworkObjectReference, spawnParams: SpawnParams) =
        if not this.IsHost then
            spawned <- true
            spawnMirageLocal mirageReference spawnParams