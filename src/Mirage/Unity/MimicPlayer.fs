module Mirage.Unity.MimicPlayer

open System
open FSharpPlus
open Unity.Netcode
open GameNetcodeStuff
open Mirage.Core.Field
open Mirage.Core.Config
open Mirage.Core.Logger

let private get<'A> = getter<'A> "MimicPlayer"

/// <summary>
/// A component that attaches to an <b>EnemyAI</b> to mimic a player.
/// If the attached enemy is a <b>MaskedPlayerEnemy</b>, this will also copy its visuals.
/// </summary>
[<AllowNullLiteral>]
type MimicPlayer() as self =
    inherit NetworkBehaviour()

    let random = new Random()

    let MimickingPlayer = field()
    let EnemyAI = field()
    let getEnemyAI = get EnemyAI "EnemyAI"

    let randomPlayer () =
        let round = StartOfRound.Instance
        let playerId = random.Next <| round.connectedPlayersAmount + 1
        round.allPlayerScripts[playerId]

    let mimicPlayer (player: PlayerControllerB) redirectToEnemy (maskedEnemy: MaskedPlayerEnemy) =
        if not <| isNull maskedEnemy then
            maskedEnemy.mimickingPlayer <- player
            maskedEnemy.SetSuit player.currentSuitID
            maskedEnemy.SetEnemyOutside(not player.isInsideFactory)
            maskedEnemy.SetVisibilityOfMaskedEnemy()
            if redirectToEnemy then
                player.redirectToEnemy <- maskedEnemy
            if not <| isNull player.deadBody then
                player.deadBody.DeactivateBody false

    let mimicEnemyEnabled (enemyAI: EnemyAI) =
        let config = getConfig()
        match enemyAI with
            | :? DressGirlAI -> false // DressGirlAI sets the mimicking player after choosing who to haunt.
            | :? BaboonBirdAI -> config.enableBaboonHawk
            | :? FlowermanAI -> config.enableBracken
            | :? SandSpiderAI -> config.enableSpider
            | :? DocileLocustBeesAI -> config.enableLocustSwarm
            | :? RedLocustBees -> config.enableBees
            | :? SpringManAI -> config.enableCoilHead
            | :? SandWormAI -> config.enableEarthLeviathan
            | :? MouthDogAI -> config.enableEyelessDog
            | :? ForestGiantAI -> config.enableForestKeeper
            | :? HoarderBugAI -> config.enableHoardingBug
            | :? BlobAI -> config.enableHygrodere
            | :? JesterAI -> config.enableJester
            | :? DoublewingAI -> config.enableManticoil
            | :? NutcrackerEnemyAI -> config.enableNutcracker
            | :? CentipedeAI -> config.enableSnareFlea
            | :? PufferAI -> config.enableSporeLizard
            | :? CrawlerAI -> config.enableThumper
            | _ -> config.enableModdedEnemies

    let logInstance message = 
        handleResult <| monad' {
            let! enemyAI = getEnemyAI "logInstance"
            logInfo $"{enemyAI.GetType().Name}({self.GetInstanceID()}) - {message}"
        }

    member this.Awake() =
        setNullable EnemyAI <| this.GetComponent<EnemyAI>()

    member this.Start() =
        ignore <| monad' {
            if this.IsHost then
                let! enemyAI = getValue EnemyAI
                let maskedEnemy = this.GetComponent<MaskedPlayerEnemy>()
                let! (player, redirectToEnemy) =
                    if (enemyAI : EnemyAI) :? MaskedPlayerEnemy then
                        if isNull maskedEnemy.mimickingPlayer then Some (randomPlayer(), false)
                        else Some (maskedEnemy.mimickingPlayer, true)
                    else if mimicEnemyEnabled enemyAI then Some (randomPlayer(), false)
                    else None
                let playerId = int player.playerClientId
                this.MimicPlayer(playerId, redirectToEnemy)
        }

    /// <summary>
    /// Mimic the given player locally. An attached <b>MimicVoice</b> automatically uses the mimicked player for voices.
    /// </summary>
    member this.MimicPlayer(playerId, redirectToEnemy) =
        let player = StartOfRound.Instance.allPlayerScripts[playerId]
        logInstance $"Mimicking player #{player.playerClientId}"
        setNullable MimickingPlayer player
        mimicPlayer player redirectToEnemy <| this.GetComponent<MaskedPlayerEnemy>()
        if this.IsHost then
            this.MimicPlayerClientRpc(playerId, redirectToEnemy)

    [<ClientRpc>]
    member this.MimicPlayerClientRpc(playerId, redirectToEnemy) =
        if not this.IsHost then
            this.MimicPlayer(playerId, redirectToEnemy)

    member this.ResetMimicPlayer() =
        logInstance "No longer mimicking a player."
        setNone MimickingPlayer
        if this.IsHost then
            this.ResetMimicPlayerClientRpc()

    [<ClientRpc>]
    member this.ResetMimicPlayerClientRpc() =
        if not this.IsHost then
            this.ResetMimicPlayer()

    member _.GetMimickingPlayer() = getValue MimickingPlayer

    member this.Update() =
        ignore <| monad' {
            if this.IsHost then
                // Set the mimicking player after the haunting player changes.
                // In singleplayer, the haunting player will always be the local player.
                // In multiplayer, the haunting player will always be the non-local player.
                let! enemyAI = getEnemyAI "Update" 
                if (enemyAI : EnemyAI) :? DressGirlAI then
                    let dressGirlAI = enemyAI :?> DressGirlAI
                    let round = StartOfRound.Instance

                    let rec randomPlayerNotHaunted () =
                        let player = randomPlayer()
                        if player = dressGirlAI.hauntingPlayer then
                            randomPlayerNotHaunted()
                        else
                            int player.playerClientId

                    match (Option.ofObj dressGirlAI.hauntingPlayer, getValue MimickingPlayer) with
                        | (Some hauntingPlayer, Some mimickingPlayer) when hauntingPlayer = mimickingPlayer && round.connectedPlayersAmount > 0 ->
                            this.MimicPlayer(randomPlayerNotHaunted(), false)
                        | (Some hauntingPlayer, None) ->
                            if round.connectedPlayersAmount = 0 then
                                this.MimicPlayer(int hauntingPlayer.playerClientId, false)
                            else
                                this.MimicPlayer(randomPlayerNotHaunted(), false)
                        | (None, Some _) ->
                            this.ResetMimicPlayer()
                        | _ -> ()
        }