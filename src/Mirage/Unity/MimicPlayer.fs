module Mirage.Unity.MimicPlayer

open System
open FSharpPlus
open Unity.Netcode
open GameNetcodeStuff
open Mirage.Core.Field
open Mirage.Core.Config

/// <summary>
/// A component that attaches to an <b>EnemyAI</b> to mimic a player.
/// If the attached enemy is a <b>MaskedPlayerEnemy</b>, this will also copy its visuals.
/// </summary>
type MimicPlayer () =
    inherit NetworkBehaviour()

    let random = new Random()
    let MimickingPlayer = field()

    let mimicPlayer (player: PlayerControllerB) (maskedEnemy: MaskedPlayerEnemy) =
        if not <| isNull maskedEnemy then
            maskedEnemy.mimickingPlayer <- player
            maskedEnemy.SetSuit player.currentSuitID
            maskedEnemy.SetEnemyOutside(not player.isInsideFactory)
            maskedEnemy.SetVisibilityOfMaskedEnemy()
            player.redirectToEnemy <- maskedEnemy
            if not <| isNull player.deadBody then
                player.deadBody.DeactivateBody false

    let mimicEnemyEnabled (enemyAI: EnemyAI) =
        let config = getConfig()
        match enemyAI with
            | :? BaboonBirdAI -> config.enableBaboonHawk
            | :? FlowermanAI -> config.enableBracken
            | :? SandSpiderAI -> config.enableSpider
            | :? DocileLocustBeesAI -> config.enableBees
            | :? SpringManAI -> config.enableCoilHead
            | :? SandWormAI -> config.enableEarthLeviathan
            | :? MouthDogAI -> config.enableEyelessDog
            | :? ForestGiantAI -> config.enableForestKeeper
            | :? DressGirlAI -> config.enableGhostGirl
            | :? HoarderBugAI -> config.enableHoardingBug
            | :? BlobAI -> config.enableHygrodere
            | :? JesterAI -> config.enableJester
            | :? DoublewingAI -> config.enableManticoil
            | :? NutcrackerEnemyAI -> config.enableNutcracker
            | :? CentipedeAI -> config.enableSnareFlea
            | :? PufferAI -> config.enableSporeLizard
            | :? CrawlerAI -> config.enableThumper
            | _ -> config.enableModdedEnemies

    member this.Start() =
        ignore <| monad' {
            if this.IsHost then
                let round = StartOfRound.Instance
                let enemyAI = this.GetComponent<EnemyAI>()
                let maskedEnemy = this.GetComponent<MaskedPlayerEnemy>()
                let! player =
                    let randomPlayer () = 
                        let playerId = random.Next <| round.connectedPlayersAmount + 1
                        Some <| StartOfRound.Instance.allPlayerScripts[playerId]
                    if enemyAI :? MaskedPlayerEnemy then
                        if isNull maskedEnemy.mimickingPlayer then randomPlayer()
                        else Some maskedEnemy.mimickingPlayer
                    else if mimicEnemyEnabled enemyAI then randomPlayer()
                    else None
                set MimickingPlayer player
                mimicPlayer player maskedEnemy
                this.MimicPlayerClientRpc <| int player.playerClientId
        }

    [<ClientRpc>]
    member this.MimicPlayerClientRpc(playerId) =
        if not this.IsHost then
            let maskedEnemy = this.GetComponent<MaskedPlayerEnemy>()
            let player = StartOfRound.Instance.allPlayerScripts[playerId]
            set MimickingPlayer player
            mimicPlayer player maskedEnemy 

    member _.GetMimickingPlayer() = getValue MimickingPlayer