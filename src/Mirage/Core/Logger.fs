module Mirage.Core.Logger

open BepInEx
open Mirage.PluginInfo

let private logger = Logging.Logger.CreateLogSource(pluginId)

let logInfo (message: string) = logger.LogInfo message
let logDebug (message: string) = logger.LogDebug message
let logWarning (message: string) = logger.LogWarning message
let logError (message: string) = logger.LogError message