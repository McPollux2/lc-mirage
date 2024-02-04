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
module Mirage.Patch.SpawnMirage

open FSharpPlus
open FSharpPlus.Data
open GameNetcodeStuff
open HarmonyLib
open UnityEngine
open Unity.Netcode
open System
open System.Collections.Generic
open System.Reflection.Emit
open Mirage.Core.Config
open Mirage.Core.Logger
open Mirage.Core.Field
open Mirage.Core.PlayerTracker
open Mirage.Unity.Network
open Mirage.Unity.MirageSpawner

let private get<'A> : Getter<'A> = getter "SpawnMirage"

type SpawnMirage() =
    static let Mask = field<HauntedMaskItem>()
    static let PlayerTracker = field()
    
    static let getMask = get Mask "Mask"
    static let getPlayerTracker = get PlayerTracker "PlayerTracker"

    static let connectedPlayers = new Dictionary<uint64, PlayerControllerB>()
    static let defaultTracker = defaultPlayerTracker (Some << _.actualClientId)
    static let random = new Random()

    [<HarmonyPostfix>]
    [<HarmonyPatch(typeof<GameNetworkManager>, "Start")>]
    static member ``save mask prefab for later use``(__instance: GameNetworkManager) =
        handleResult <| monad' {
            let! mask =
                findNetworkPrefab<HauntedMaskItem> (__instance.GetComponent<NetworkManager>())
                    |> Option.toResultWith "HauntedMaskItem network prefab is missing. This is likely due to a mod incompatibility."
            set Mask mask
        }

    [<HarmonyPostfix>]
    [<HarmonyPatch(typeof<StartOfRound>, "OnLocalDisconnect")>]
    static member ``reset connected players when game is finished``(__instance: StartOfRound) =
        connectedPlayers.Clear()
        setNone PlayerTracker

    [<HarmonyPostfix>]
    [<HarmonyPatch(typeof<PlayerControllerB>, "ConnectClientToPlayerObject")>]
    static member ``start player manager (host)``(__instance: PlayerControllerB) =
        let isLocalPlayer () =
            not (isNull GameNetworkManager.Instance.localPlayerController)
                && __instance = GameNetworkManager.Instance.localPlayerController 
        if __instance.IsHost && isLocalPlayer() then
            // This will always be the host player.
            connectedPlayers[__instance.actualClientId] <- __instance
            let playerTracker = defaultTracker
            set PlayerTracker <| addPlayer playerTracker __instance

    [<HarmonyPostfix>]
    [<HarmonyPatch(typeof<StartOfRound>, "OnClientConnect")>]
    static member ``start tracking player on connect``(__instance: StartOfRound, clientId: uint64) =
        handleResult <| monad' {
            if __instance.IsHost then
                // This will always be a non-host player.
                let playerId = StartOfRound.Instance.ClientPlayerList[clientId]
                let player = StartOfRound.Instance.allPlayerScripts[playerId]
                connectedPlayers[clientId] <- player
                let! playerTracker = getPlayerTracker "``stop tracking player on connect``"
                set PlayerTracker <| addPlayer playerTracker player
        }

    [<HarmonyPostfix>]
    [<HarmonyPatch(typeof<StartOfRound>, "OnClientDisconnect")>]
    static member ``stop tracking player on disconnect``(__instance: StartOfRound, clientId: uint64) =
        handleResult <| monad' {
            if __instance.IsHost then
                ignore <| connectedPlayers.Remove clientId
                let playerId = StartOfRound.Instance.ClientPlayerList[clientId]
                let player = StartOfRound.Instance.allPlayerScripts[playerId]
                let! playerTracker = getPlayerTracker "``stop tracking player on disconnect``"
                set PlayerTracker <| removePlayer playerTracker player
        }

    [<HarmonyPostfix>]
    [<HarmonyPatch(typeof<StartOfRound>, "StartGame")>]
    static member ``start player tracker, and add players if they're already connected``(__instance: StartOfRound) =
        handleResult <| monad' {
            if __instance.IsHost then
                if connectedPlayers.Count > 0 then
                    let mutable playerTracker = defaultTracker
                    flip iter connectedPlayers <| fun player ->
                        playerTracker <- addPlayer playerTracker player
                    set PlayerTracker playerTracker
        }

    [<HarmonyPrefix>]
    [<HarmonyPatch(typeof<PlayerControllerB>, "KillPlayerServerRpc")>]
    static member ``spawn mirage on player death``(__instance: PlayerControllerB) =
        handleResult <| monad' {
            if __instance.IsHost then
                // For whatever reason, KillPlayerServerRpc is invoked twice, per player death.
                // PlayerTracker is used to ensure spawning only happens once.
                let! playerTracker = getPlayerTracker "``spawn mirage on player death``"
                if isPlayerTracked playerTracker __instance then
                    set PlayerTracker <| removePlayer playerTracker __instance
                    if random.Next(1, 101) <= getConfig().spawnOnPlayerDeath then
                        let! maskPrefab = getMask "``spawn mirage on player death``"
                        let mask = UnityEngine.Object.Instantiate<GameObject>(maskPrefab.gameObject).GetComponent<HauntedMaskItem>()
                        mask.transform.localScale <- Vector3.zero
                        mask.previousPlayerHeldBy <- __instance
                        mask.NetworkObject.Spawn()
                        let mirageSpawner = __instance.GetComponent<MirageSpawner>()
                        mirageSpawner.SpawnMirage mask
                        mask.NetworkObject.Despawn()
        }

    [<HarmonyPrefix>]
    [<HarmonyPatch(typeof<HauntedMaskItem>, "CreateMimicServerRpc")>]
    static member ``use mirage spawner instead of default create mimic server rpc``(__instance: HauntedMaskItem) =
        if __instance.IsHost && not __instance.previousPlayerHeldBy.isPlayerDead then
            __instance.previousPlayerHeldBy.KillPlayer(Vector3.zero, false, CauseOfDeath.Suffocation, __instance.maskTypeId)
        false

    [<HarmonyPrefix>]
    [<HarmonyPatch(typeof<PlayerControllerB>, "SpawnDeadBody")>]
    static member ``disable player corpse spawn``(__instance: PlayerControllerB) =
        not <| __instance.GetComponent<MirageSpawner>().IsSpawned()

    [<HarmonyPrefix>]
    [<HarmonyPatch(typeof<MaskedPlayerEnemy>)>]
    [<HarmonyPatch("SetHandsOutServerRpc")>]
    [<HarmonyPatch("SetHandsOutClientRpc")>]
    static member ``disable mirage hands out``() = getConfig().enableArmsOut

    [<HarmonyPostfix>]
    [<HarmonyPatch(typeof<MaskedPlayerEnemy>, "Start")>]
    static member ``remove mask texture and mimic a random player if enabled``(__instance: MaskedPlayerEnemy) =
        let player = __instance.mimickingPlayer
        if isNull player then
            let round = StartOfRound.Instance
            let players = round.allPlayerScripts
            let playerId = random.Next <| round.connectedPlayersAmount + 1
            let player = players[playerId]
            setMiragePlayer __instance player
        if not <| getConfig().enableMask then
            __instance.GetComponentsInChildren<Transform>()
                |> filter _.name.StartsWith("HeadMask")
                |> iter _.gameObject.SetActive(false)

    /// <summary>
    /// Since MaskedPlayerEnemy#killAnimation spawns a mimic, this patch finds the spawn instructions and disables it.<br />
    /// This patch is not automatically picked up by harmony, requiring a manual call to <b>Harmony#Patch</b>.
    /// </summary>
    static member ``prevent duplicate mirage spawn``(instructions: IEnumerable<CodeInstruction>): IEnumerable<CodeInstruction> =
        let targetMethod = AccessTools.Method(typeof<NetworkBehaviour>, "get_IsServer")
        seq {
            for instruction in instructions do
                if instruction.Calls targetMethod then
                    // Skip the if (base.IsServer) { ... } call.
                    yield new CodeInstruction(OpCodes.Pop)
                    yield new CodeInstruction(OpCodes.Ldc_I4_0)

                    // Since this is normally within the now skipped if statement, we need to add this back:
                    // this.playersKilled.Add(int this.inSpecialAnimationWithPlayer.playerClientId)
                    yield new CodeInstruction(OpCodes.Ldloc_1)
                    yield new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof<MaskedPlayerEnemy>, "playersKilled"))
                    yield new CodeInstruction(OpCodes.Ldloc_1)
                    yield new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof<EnemyAI>, "inSpecialAnimationWithPlayer"))
                    yield new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof<PlayerControllerB>, "playerClientId"))
                    yield new CodeInstruction(OpCodes.Conv_I4)
                    yield new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof<List<int32>>, "Add"))
                else
                    yield instruction
        }