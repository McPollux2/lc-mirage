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
module Mirage.Core.Audio.Network.Client

open FSharpPlus
open UnityEngine
open NAudio.Wave
open Microsoft.FSharp.Data.UnitSystems.SI.UnitNames
open System
open System.Threading.Tasks
open Mirage.Core.Audio.Data
open Mirage.PluginInfo
open Mirage.Core.Audio.Format
open Mirage.Core.Logger

/// <summary>
/// Handles the client side of audio streaming.
/// </summary>
type AudioClient =
    private
        {   audioSource: AudioSource
            pcmHeader: PcmHeader
            decompressor: IMp3FrameDecompressor
            mutable startTime: int64
            mutable timeoutEnabled: bool
            mutable stopped: bool
        }

/// <summary>
/// Stop the audio client. This must be called to cleanup resources.
/// </summary>
let stopClient (client: AudioClient) =
    if not client.stopped then
        client.stopped <- true
        client.audioSource.Stop()
        UnityEngine.Object.Destroy client.audioSource.clip
        client.audioSource.clip <- null
        dispose client.decompressor

/// <summary>
/// Start receiving audio data from the server, and playing it back live.
/// 
/// Note: This will not stop the <b>AudioSource</b> if it's currently playing.
/// You will need to handle that yourself at the callsite.
/// </summary>
let startClient (audioSource: AudioSource) (pcmHeader: PcmHeader) : AudioClient =
    audioSource.clip <-
        AudioClip.Create(
            pluginId,
            pcmHeader.samples,
            pcmHeader.channels,
            pcmHeader.frequency,
            false
        )
    ignore <| audioSource.clip.SetData(Array.zeroCreate(pcmHeader.samples * pcmHeader.channels), 0)
    let waveFormat =
        new Mp3WaveFormat(
            pcmHeader.frequency,
            pcmHeader.channels,
            pcmHeader.blockSize,
            pcmHeader.bitRate
        )
    {   audioSource = audioSource
        pcmHeader = pcmHeader
        decompressor = new AcmMp3FrameDecompressor(waveFormat)
        startTime = 0
        timeoutEnabled = false
        stopped = false
    }

/// <summary>
/// Set the audio client frame data, and play it if the audio source hasn't started yet.
/// </summary>
let setFrameData (client: AudioClient) (frameData: FrameData) =
    try
        let pcmData = convertFrameToPCM client.decompressor frameData.rawData
        if pcmData.Length > 0 then
            ignore <| client.audioSource.clip.SetData(pcmData, frameData.sampleIndex)
        if not client.audioSource.isPlaying then
            client.audioSource.Play()
        client.startTime <- DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
    with | error ->
        logError $"Failed to set frame data: {error}"
        stopClient client

/// <summary>
/// Set a timeout for the acceptable amount of time in between <b>setFrameData</b> calls.
/// If the timeout is exceeded, the client will stop.
/// </summary>
let startTimeout (client: AudioClient) (timeout: int<second>) : Task<Unit> =
    task {
        client.timeoutEnabled <- true
        let timeoutMs = int64 timeout * 1000L
        client.startTime <- DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        let mutable currentTime = client.startTime
        while not client.stopped && client.timeoutEnabled && currentTime - client.startTime < timeoutMs do
            do! Async.Sleep 1000
            currentTime <- DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        if not client.stopped && client.timeoutEnabled then
            logError $"AudioClient timed out after not receiving frame data for {timeout} seconds."
            stopClient client
    }

/// <summary>
/// Disable the timeout started by <b>startTimeout</b>.
/// </summary>
let stopTimeout (client: AudioClient) = client.timeoutEnabled <- false