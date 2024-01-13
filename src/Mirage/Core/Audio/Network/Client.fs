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
module Mirage.Unity.Audio.Network.Client

open System
open UnityEngine
open NAudio.Wave
open Mirage.Core.Audio.Data
open Mirage.PluginInfo
open FSharpPlus

/// <summary>
/// Handles the client side of audio streaming.
/// </summary>
type AudioClient =
    {   audioSource: AudioSource
        pcmHeader: PcmHeader
        decompressor: IMp3FrameDecompressor
    }

/// <summary>
/// Stop the audio client. This must be called to cleanup resources.
/// </summary>
let stopClient (client: AudioClient) =
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
    }