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
module Mirage.Core.Logger

open BepInEx
open Mirage.PluginInfo

let private logger = Logging.Logger.CreateLogSource(pluginId)

let logInfo (message: string) = logger.LogInfo message
let logDebug (message: string) = logger.LogDebug message
let logWarning (message: string) = logger.LogWarning message
let logError (message: string) = logger.LogError message