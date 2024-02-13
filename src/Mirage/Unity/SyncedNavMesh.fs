module Mirage.Unity.SyncedNavMesh

open FSharpPlus
open Unity.Netcode
open UnityEngine.AI
open Mirage.Core.Field

/// <summary>
/// Syncs nav mesh agent state for all clients.
/// </summary>
[<AllowNullLiteral>]
type SyncedNavMesh() as self =
    inherit NetworkBehaviour()

    let mutable previousIsOnNavMesh = false
    let mutable isOnNavMesh = false

    let NavMeshAgent = field<NavMeshAgent>()

    let updateNavMesh () =
        flip iter (getValue NavMeshAgent) <| fun agent ->
            if self.IsHost then
                if previousIsOnNavMesh <> agent.isOnNavMesh then
                    isOnNavMesh <- agent.isOnNavMesh
                    self.SetOnNavMeshClientRpc agent.isOnNavMesh
                previousIsOnNavMesh <- agent.isOnNavMesh

    member this.Start() =
        setNullable NavMeshAgent <| this.GetComponent<NavMeshAgent>()
        updateNavMesh()

    member _.IsOnNavMesh() = isOnNavMesh

    [<ClientRpc>]
    member _.SetOnNavMeshClientRpc(onNavMesh: bool) =
        isOnNavMesh <- onNavMesh

    member _.Update() = updateNavMesh()