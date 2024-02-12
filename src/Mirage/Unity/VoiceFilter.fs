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
module Mirage.Unity.VoiceFilter

open FSharpPlus
open UnityEngine
open Mirage.Core.Config
open Mirage.Core.Field
open Mirage.Core.Logger
open Mirage.Unity.AudioStream
open Mirage.Unity.MimicPlayer

let private get<'A> = getter<'A> "VoiceFilter"

/// <summary>
/// Filters the local audio source to sound more like the vanilla player voices.
/// </summary>
type VoiceFilter() as self =
    inherit MonoBehaviour()

    let mutable checkInterval = Random.Range(0f, 0.4f)
    let mutable occluded = false

    let EnemyAI = field<EnemyAI>()
    let MimicPlayer = field<MimicPlayer>()
    let AudioStream = field<AudioStream>()
    let LowPassFilter = field<AudioLowPassFilter>()
    let ReverbFilter = field<AudioReverbFilter>()

    let getEnemyAI = get EnemyAI "EnemyAI"
    let getMimicPlayer = get MimicPlayer "MimicPlayer"
    let getAudioStream = get AudioStream "AudioStream"
    let getLowPassFilter = get LowPassFilter "LowPassFilter"
    let getReverbFilter = get ReverbFilter "ReverbFilter"

    let isOccluded () =
        StartOfRound.Instance <> null
            && Physics.Linecast(self.transform.position, StartOfRound.Instance.audioListener.transform.position, 256, QueryTriggerInteraction.Ignore)

    let mute () =
        ignore <| monad' {
            let! audioStream = getValue AudioStream
            audioStream.GetAudioSource().mute <- true
        }

    member this.Start() =
        set EnemyAI <| this.GetComponent<EnemyAI>()
        set MimicPlayer <| this.GetComponent<MimicPlayer>()
        let audioStream = this.GetComponent<AudioStream>()
        set AudioStream audioStream
        let audioSource = audioStream.GetAudioSource()
        audioSource.dopplerLevel <- 0f
        audioSource.maxDistance <- 50f
        audioSource.minDistance <- 6f
        audioSource.priority <- 0
        audioSource.spread <- 30f
        audioSource.spatialBlend <- 1f

        let lowPassFilter =
            let filter = audioSource.GetComponent<AudioLowPassFilter>()
            if isNull filter then audioSource.gameObject.AddComponent<AudioLowPassFilter>()
            else filter
        lowPassFilter.cutoffFrequency <- 20000f
        set LowPassFilter lowPassFilter

        let reverbFilter =
            let filter = audioSource.GetComponent<AudioReverbFilter>()
            if isNull filter then audioSource.gameObject.AddComponent<AudioReverbFilter>()
            else filter
        reverbFilter.reverbPreset <- AudioReverbPreset.User
        reverbFilter.dryLevel <- -1f
        reverbFilter.decayTime <- 0.8f
        reverbFilter.room <- -2300f
        setNullable ReverbFilter reverbFilter

        occluded <- isOccluded()

    member this.Update() =
        handleResultWith mute <| monad' {
            let methodName = "Update"
            let! audioStream = getAudioStream methodName
            let audioSource = audioStream.GetAudioSource()
            let! lowPassFilter = getLowPassFilter methodName
            let! reverbFilter = getReverbFilter methodName
            let! enemyAI = getEnemyAI methodName
            let round = StartOfRound.Instance
            let localPlayer = round.localPlayerController
            let maskedEnemyIsHiding () = enemyAI :? MaskedPlayerEnemy && (enemyAI :?> MaskedPlayerEnemy).crouching
            let! mimicPlayer = getMimicPlayer methodName
            match mimicPlayer.GetMimickingPlayer() with
                | None -> audioSource.mute <- true
                | Some mimickingPlayer ->
                    let isMimicLocalPlayerMuted () =
                        getConfig().muteLocalPlayerVoice
                            && mimickingPlayer = localPlayer
                            && not mimickingPlayer.isPlayerDead
                    let isNotHauntedOrDisappearedDressGirl () =
                        enemyAI :? DressGirlAI && (
                            let dressGirlAI = enemyAI :?> DressGirlAI
                            let isVisible = dressGirlAI.staringInHaunt || dressGirlAI.moveTowardsDestination && dressGirlAI.movingTowardsTargetPlayer
                            not <| dressGirlAI.hauntingLocalPlayer || not isVisible
                        )
                    if enemyAI.isEnemyDead
                        || maskedEnemyIsHiding()
                        || isMimicLocalPlayerMuted()
                        || isNotHauntedOrDisappearedDressGirl()
                    then
                        audioSource.mute <- true
                    else if enemyAI.isOutside then
                        reverbFilter.enabled <- false
                        audioSource.mute <- localPlayer.isInsideFactory
                    else
                        audioSource.mute <- not localPlayer.isInsideFactory
                        let listenerPosition = round.audioListener.transform.position
                        let distanceToListener = Vector3.Distance(listenerPosition, this.transform.position)
                        let normalizedDistanceReverb = 0f - 3.4f * distanceToListener / audioSource.maxDistance / 5f
                        let clampedDryLevel = Mathf.Clamp(normalizedDistanceReverb, -300f, -1f)
                        let lerpFactorReverb = Time.deltaTime * 8f
                        reverbFilter.dryLevel <-
                            Mathf.Lerp(
                                reverbFilter.dryLevel,
                                clampedDryLevel,
                                lerpFactorReverb
                            )
                        reverbFilter.enabled <- true

                    if occluded then
                        let distance = Vector3.Distance(StartOfRound.Instance.audioListener.transform.position, this.transform.position)
                        let normalizedDistance = 2500f / distance / audioSource.maxDistance / 2f
                        let clampedFrequency = Mathf.Clamp(normalizedDistance, 900f, 4000f)
                        let lerpFactor = Time.deltaTime * 8f
                        lowPassFilter.cutoffFrequency <- Mathf.Lerp(lowPassFilter.cutoffFrequency, clampedFrequency, lerpFactor)
                    else
                        lowPassFilter.cutoffFrequency <- Mathf.Lerp(lowPassFilter.cutoffFrequency, 10000f, Time.deltaTime * 8f);
                    
                    if checkInterval >= 0.5f then
                        checkInterval <- 0f
                        occluded <- isOccluded()
                    else
                        checkInterval <- checkInterval + Time.deltaTime
            }