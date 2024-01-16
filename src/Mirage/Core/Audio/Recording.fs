module Mirage.Core.Audio.Recording

open FSharpPlus
open GameNetcodeStuff
open System;
open System.IO
open Mirage.Core.File
open Mirage.Core.Logger
open Microsoft.FSharp.Core.Option

/// <summary>
/// Delete the audio directory, ignoring the <b>IOException</b> if it gets thrown.
/// </summary>
let deleteRecordings () =
    try
        Directory.Delete(RootDirectory + AudioDirectory, true)
    with
        | :? IOException as _ -> ()
        | error -> raise error

/// <summary>
/// Get the file paths of all recordings for the given player.
/// </summary>
let getRecordings (playerAudioId: string) : array<string> =
    let directory = getRecordingsPath playerAudioId
    try
        Directory.GetFiles directory
    with | error ->
        logError $"Failed to load directory: {directory}"
        logError $"{error}"
        zero

/// <summary>
/// Create a file path to save the audio recording to.
/// </summary>
let createRecordingPath (playerAudioId: string): string =
    $"{getRecordingsPath playerAudioId}/{DateTime.UtcNow.ToFileTime()}"

type RecordingManager =
    private
        {   // Alive players using their player audio id as the key.
            alivePlayers: Map<string, PlayerControllerB>
        }

/// <summary>
/// Initialize the recording manager. This should be called at the start of
/// a round, when all player scripts are ready (have been added to the list).
/// </summary>
let startRecordingManager (playerManager: StartOfRound) =
    {   alivePlayers =
            playerManager.allPlayerScripts
                |> Array.map (fun player -> (player.voicePlayerState.Name, player))
                |> List.ofSeq
                |> Map.ofList
    }

/// <summary>
/// Mark the player as dead to avoid recording the player's voice.
/// </summary>
let setPlayerDead (manager: RecordingManager) (player: PlayerControllerB) =
    { manager with
        alivePlayers = Map.remove player.voicePlayerState.Name <| manager.alivePlayers
    }

/// <summary>
/// Whether the given player is still alive or not.
/// </summary>
let isPlayerAlive (manager: RecordingManager) (playerAudioId: string) : bool =
    isSome <| manager.alivePlayers.TryFind playerAudioId