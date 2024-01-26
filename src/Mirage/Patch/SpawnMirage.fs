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
open System.Collections.Generic
open System.Reflection.Emit
open Mirage.Core.Logger
open Mirage.Core.Field
open Mirage.Core.PlayerTracker
open Mirage.Unity.Network
open Mirage.Unity.Enemy.MirageSpawner

let private get<'A> : Getter<'A> = getter "SpawnMirage"

type SpawnMirage() =
    static let MaskItemPrefab = field<HauntedMaskItem>()
    static let PlayerTracker = field()
    
    static let getMaskItemPrefab = get MaskItemPrefab "MaskItemPrefab"
    static let getPlayerTracker = get PlayerTracker "PlayerTracker"

    /// <summary>
    /// Spawn a mirage at the player's location.
    /// </summary>
    static let spawnMirage (player: PlayerControllerB) =
        handleResult <| monad' {
            let! maskPrefab = getMaskItemPrefab "``spawn mirage enemy on player death``"
            let maskItem = UnityEngine.Object.Instantiate<GameObject>(maskPrefab.gameObject).GetComponent<HauntedMaskItem>()
            maskItem.transform.localScale <- Vector3.zero
            maskItem.previousPlayerHeldBy <- player
            maskItem.NetworkObject.Spawn()
            let mirageSpawner = maskItem.GetComponent<MirageSpawner>()
            mirageSpawner.SetMaskItem maskItem
            mirageSpawner.SpawnMirage()
            maskItem.NetworkObject.Despawn()
        }

    [<HarmonyPostfix>]
    [<HarmonyPatch(typeof<PlayerControllerB>, "ConnectClientToPlayerObject")>]
    static member ``start player manager (host)``(__instance: PlayerControllerB) =
        // Only start the player manager after game network manager has set the local player controller.
        if __instance.IsHost 
            && not (isNull GameNetworkManager.Instance.localPlayerController)
            && __instance = GameNetworkManager.Instance.localPlayerController 
            && Option.isNone PlayerTracker.Value
                then
                defaultPlayerTracker (Some << _.actualClientId)
                    |> flip addPlayer GameNetworkManager.Instance.localPlayerController
                    |> set PlayerTracker

    [<HarmonyPostfix>]
    [<HarmonyPatch(typeof<StartOfRound>, "OnClientConnect")>]
    static member ``start tracking player on game start``(__instance: StartOfRound, clientId: uint64) =
        if __instance.IsHost then
            let playerId = StartOfRound.Instance.ClientPlayerList[clientId]
            let player = StartOfRound.Instance.allPlayerScripts[playerId]
            getPlayerTracker zero
                |>> flip addPlayer player
                |> Option.ofResult
                |> setOption PlayerTracker

    [<HarmonyPostfix>]
    [<HarmonyPatch(typeof<GameNetworkManager>, "Start")>]
    static member ``save mask prefab for later use``(__instance: GameNetworkManager) =
        handleResult <| monad' {
            let! maskItem =
                findNetworkPrefab<HauntedMaskItem> (__instance.GetComponent<NetworkManager>())
                    |> Option.toResultWith "HauntedMaskItem network prefab is missing. This is likely due to a mod incompatibility."
            set MaskItemPrefab maskItem
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
                    spawnMirage __instance
        }

    [<HarmonyPrefix>]
    [<HarmonyPatch(typeof<HauntedMaskItem>, "CreateMimicServerRpc")>]
    static member ``use mirage spawner instead of default create mimic server rpc``(__instance: HauntedMaskItem) =
        if __instance.IsHost then
            if not __instance.previousPlayerHeldBy.isPlayerDead then
                __instance.previousPlayerHeldBy.KillPlayer(Vector3.zero, false, CauseOfDeath.Suffocation, __instance.maskTypeId)
        false

    [<HarmonyPrefix>]
    [<HarmonyPatch(typeof<PlayerControllerB>, "SpawnDeadBody")>]
    static member ``disable player corpse spawn``() = false

    [<HarmonyPrefix>]
    [<HarmonyPatch(typeof<MaskedPlayerEnemy>)>]
    [<HarmonyPatch("SetHandsOutServerRpc")>]
    [<HarmonyPatch("SetHandsOutClientRpc")>]
    static member ``disable mirage hands out``() = false

    [<HarmonyPostfix>]
    [<HarmonyPatch(typeof<MaskedPlayerEnemy>, "Start")>]
    static member ``remove mask texture``(__instance: MaskedPlayerEnemy) =
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