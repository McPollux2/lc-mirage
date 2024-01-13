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
module Mirage.Core.Audio.Data

open Unity.Netcode

/// <summary>
/// Represents raw frame data and the sample index it begins at.
/// </summary>
[<Struct>]
type FrameData =
    {   mutable rawData: array<byte>
        mutable sampleIndex: int
    }
    interface INetworkSerializable with
        member this.NetworkSerialize(serializer: BufferSerializer<'T>) : unit =
            serializer.SerializeValue(&this.rawData)
            serializer.SerializeValue(&this.sampleIndex)

/// <summary>
/// All the necessary information for PCM data to be read.
/// </summary>
[<Struct>]
type PcmHeader =
    {   mutable samples: int
        mutable channels: int
        mutable frequency: int
        mutable blockSize: int
        mutable bitRate: int
    }
    interface INetworkSerializable with
        member this.NetworkSerialize(serializer: BufferSerializer<'T>) : unit = 
            serializer.SerializeValue(&this.samples)
            serializer.SerializeValue(&this.channels)
            serializer.SerializeValue(&this.frequency)
            serializer.SerializeValue(&this.blockSize)
            serializer.SerializeValue(&this.bitRate)