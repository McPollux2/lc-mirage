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
module Mirage.Patch.SyncConfig

open HarmonyLib
open GameNetcodeStuff
open Unity.Netcode
open Mirage.Core.Config

type SyncConfig () =
    [<HarmonyPostfix>]
    [<HarmonyPatch(typeof<PlayerControllerB>, "ConnectClientToPlayerObject")>]
    static member ``synchronize config when joining a game``() =
        if NetworkManager.Singleton.IsHost then
            registerHandler RequestSync
        else
            registerHandler ReceiveSync
            requestSync()

    [<HarmonyPostfix>]
    [<HarmonyPatch(typeof<GameNetworkManager>, "StartDisconnect")>]
    static member ``desynchronize config after leaving the game``() =
        revertSync()