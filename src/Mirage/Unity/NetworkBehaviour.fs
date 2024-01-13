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
module Mirage.Unity.NetworkBehaviour

open Unity.Netcode

/// <summary>
/// Verify if the given <b>ServerRpcParams</b> contains a valid <b>ServerClientId</b>.
/// This is intended to be used with ServerRpc methods that do not require ownership to invoke.
/// </summary>
let isValidClient (behaviour: NetworkBehaviour) (serverParams: ServerRpcParams) =
    behaviour.NetworkManager.ConnectedClients.ContainsKey serverParams.Receive.SenderClientId