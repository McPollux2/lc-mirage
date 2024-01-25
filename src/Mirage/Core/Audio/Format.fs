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
module Mirage.Core.Audio.Format

open FSharpPlus
open UnityEngine
open NAudio.Wave
open NAudio.Lame
open System
open System.IO
open Mirage.PluginInfo

/// <summary>
/// Converts the given MP3 frame data to PCM format.
/// Note: This function <i>will</i> throw an exception if invalid bytes are provided.
/// </summary>
let decompressFrame (decompressor: IMp3FrameDecompressor) (frameData: array<byte>) : array<float32> =
    use stream = new MemoryStream(frameData)
    let frame = Mp3Frame.LoadFromStream stream
    let samples = Array.zeroCreate <| 16384 * 4 // Large enough buffer for a single frame.
    let bytesDecompressed = decompressor.DecompressFrame(frame, samples, 0)
    let pcmData : array<int16> = Array.zeroCreate bytesDecompressed
    Buffer.BlockCopy(samples, 0, pcmData, 0, bytesDecompressed)
    flip (/) 32768.0f << float32 <!> pcmData

/// <summary>
/// Convert the given <b>.wav</b> audio file to a <b>.mp3</b> in-memory.<br />
/// Warning: You will need to call <b>Mp3FileReader#Dispose</b> and <b>Mp3fileReader#mp3Stream#Dispose</b> yourself.
/// </summary>
let compressAudio (audioReader: AudioFileReader) : Mp3FileReader =
    let mp3Stream = new MemoryStream()
    use mp3Writer = new LameMP3FileWriter(mp3Stream, audioReader.WaveFormat, LAMEPreset.STANDARD)
    audioReader.CopyTo mp3Writer
    mp3Writer.Flush()
    mp3Stream.Position <- 0
    new Mp3FileReader(mp3Stream)

/// <summary>
/// Convert the given <b>.mp3</b> audio file to a <b>.wav</b> in-memory.<br />
/// Warning: You will need to call <b>WaveFileReader#Dispose</b> and <b>WaveFileReader#mp3Stream#Dispose</b> yourself.
/// </summary>
let decompressAudio (audioReader: Mp3FileReader) : WaveFileReader =
    let waveStream = new MemoryStream()
    let waveWriter = new WaveFileWriter(waveStream, audioReader.WaveFormat)
    audioReader.CopyTo waveWriter
    waveWriter.Flush()
    waveStream.Position <- 0
    new WaveFileReader(waveStream)

/// <summary>
/// Convert the <b>WaveFileReader</b> to an <b>AudioClip</b>.
/// </summary>
let convertToAudioClip (audioReader: WaveFileReader) : AudioClip = 
    let sampleProvider = audioReader.ToSampleProvider()
    let samples = Array.zeroCreate << int <| audioReader.SampleCount
    ignore <| sampleProvider.Read(samples, 0, samples.Length)
    let audioClip = AudioClip.Create(
        pluginId,
        samples.Length,
        audioReader.WaveFormat.Channels,
        audioReader.WaveFormat.SampleRate,
        false
    )
    ignore <| audioClip.SetData(samples, 0)
    audioClip