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
open Mirage.Unity.AudioStream
open Mirage.Unity.MimicPlayer

/// <summary>
/// Filters the local audio source to sound more like the vanilla player voices.
/// </summary>
type VoiceFilter() =
    inherit MonoBehaviour()

    let mutable checkInterval = Random.Range(0f, 0.4f)
    let mutable occluded = false

    let EnemyAI = field<EnemyAI>()
    let MimicPlayer = field<MimicPlayer>()
    let AudioStream = field<AudioStream>()
    let LowPassFilter = field<AudioLowPassFilter>()
    let ReverbFilter = field<AudioReverbFilter>()

    let isOccluded (this: VoiceFilter) =
        StartOfRound.Instance <> null
            && Physics.Linecast(this.transform.position, StartOfRound.Instance.audioListener.transform.position, 256, QueryTriggerInteraction.Ignore)

    member this.Start() =
        let audioStream = this.GetComponent<AudioStream>()
        set AudioStream audioStream
        let audioSource = audioStream.GetAudioSource()
        audioSource.dopplerLevel <- 0f
        audioSource.maxDistance <- 50f
        audioSource.minDistance <- 6f
        audioSource.priority <- 0
        audioSource.spread <- 30f
        audioSource.spatialBlend <- 1f

        if isNull <| audioSource.GetComponent<AudioLowPassFilter>() then
            let lowPassFilter = audioSource.gameObject.AddComponent<AudioLowPassFilter>()
            lowPassFilter.cutoffFrequency <- 20000f
            setNullable LowPassFilter lowPassFilter

        if isNull <| audioSource.GetComponent<AudioReverbFilter>() then
            let reverbFilter = audioSource.gameObject.AddComponent<AudioReverbFilter>()
            reverbFilter.reverbPreset <- AudioReverbPreset.User
            reverbFilter.dryLevel <- -1f
            reverbFilter.decayTime <- 0.8f
            reverbFilter.room <- -2300f
            setNullable ReverbFilter reverbFilter

        occluded <- isOccluded this

    member this.Update() =
        ignore <| monad' {
            let! audioStream = getValue AudioStream
            let audioSource = audioStream.GetAudioSource()
            let! lowPassFilter = getValue LowPassFilter
            let! reverbFilter = getValue ReverbFilter
            let! enemyAI = getValue EnemyAI
            let round = StartOfRound.Instance
            let localPlayer = round.localPlayerController
            let maskedEnemyIsHiding () = enemyAI :? MaskedPlayerEnemy && (enemyAI :?> MaskedPlayerEnemy).crouching
            let! mimickingPlayer = getValue MimicPlayer >>= _.GetMimickingPlayer()
            let isMimicLocalPlayerMuted () =
                getConfig().muteLocalPlayerVoice
                    && mimickingPlayer = localPlayer
                    && not mimickingPlayer.isPlayerDead
            let isNotHauntedByDressGirl () =
                if enemyAI :? DressGirlAI then
                    let dressGirlAI = enemyAI :?> DressGirlAI
                    not dressGirlAI.hauntingLocalPlayer || not dressGirlAI.enemyMeshEnabled
                else
                    false
            if enemyAI.isEnemyDead
                || maskedEnemyIsHiding()
                || isMimicLocalPlayerMuted()
                || isNotHauntedByDressGirl()
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
                occluded <- isOccluded this
            else
                checkInterval <- checkInterval + Time.deltaTime
        }