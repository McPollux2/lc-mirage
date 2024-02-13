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
module Mirage.Patch.IgnoreNavMesh

open HarmonyLib
open System.Runtime.CompilerServices
open Mirage.Unity.SyncedNavMesh

type IgnoreNavMesh() =
    static let occludeNavMesh = new ConditionalWeakTable<OccludeAudio, SyncedNavMesh>()

    [<HarmonyPrefix>]
    [<HarmonyPatch(typeof<EnemyAI>, "SetDestinationToPosition")>]
    [<HarmonyPatch(typeof<EnemyAI>, "DoAIInterval")>]
    [<HarmonyPatch(typeof<EnemyAI>, "PathIsIntersectedByLineOfSight")>]
    static member ``skip nav mesh calculations if not on nav mesh``(__instance: EnemyAI) =
        __instance.agent.isOnNavMesh

    [<HarmonyPostfix>]
    [<HarmonyPatch(typeof<OccludeAudio>, "Start")>]
    static member ``store nav mesh for later use``(__instance: OccludeAudio) =
        let navMesh = __instance.GetComponent<SyncedNavMesh>()
        if not <| isNull navMesh then
            occludeNavMesh.Add(__instance, navMesh)

    [<HarmonyPrefix>]
    [<HarmonyPatch(typeof<OccludeAudio>, "Update")>]
    static member ``skip OccludeAudio#Update if not on nav mesh``(__instance: OccludeAudio) =
        let mutable agent = null
        if occludeNavMesh.TryGetValue(__instance, &agent) then
            __instance.thisAudio.mute <- not <| agent.IsOnNavMesh()
            agent.IsOnNavMesh()
        else
            true