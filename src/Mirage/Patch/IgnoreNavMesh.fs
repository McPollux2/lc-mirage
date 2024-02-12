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

type IgnoreNavMesh() =
    [<HarmonyPrefix>]
    [<HarmonyPatch(typeof<EnemyAI>, "SetDestinationToPosition")>]
    static member ``skip Enemy#SetDestinationToPosition if not on nav mesh``(__instance: EnemyAI) =
        __instance.agent.isOnNavMesh

    [<HarmonyPrefix>]
    [<HarmonyPatch(typeof<EnemyAI>, "DoAIInterval")>]
    static member ``skip Enemy#DoAIInterval if not on nav mesh``(__instance: EnemyAI) =
        __instance.agent.isOnNavMesh

    [<HarmonyPrefix>]
    [<HarmonyPatch(typeof<EnemyAI>, "PathIsIntersectedByLineOfSight")>]
    static member ``skip Enemy#PathIsIntersectedByLineOfSight if not on nav mesh``(__instance: EnemyAI) =
        __instance.agent.isOnNavMesh