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
open NAudio.Wave
open System
open System.IO

let private decompressFrame (decompressor: IMp3FrameDecompressor) frame =
    let samples = Array.zeroCreate <| 16384 * 4 // Large enough buffer for a single frame.
    let bytesDecompressed = decompressor.DecompressFrame(frame, samples, 0)
    (bytesDecompressed, samples)

let private normalizeSamples (bytesDecompressed, samples) =
    let pcmData : array<int16> = Array.zeroCreate bytesDecompressed
    Buffer.BlockCopy(samples, 0, pcmData, 0, bytesDecompressed)
    flip (/) 32768.0f << float32 <!> pcmData

/// <summary>
/// Converts the given MP3 frame data to PCM format.
/// Note: This function <i>will</i> throw an exception if invalid bytes are provided.
/// </summary>
let convertToPCM (decompressor: IMp3FrameDecompressor) (frameData: array<byte>) : array<float32> =
    use stream = new MemoryStream(frameData)
    normalizeSamples << decompressFrame decompressor <| Mp3Frame.LoadFromStream stream