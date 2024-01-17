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
module Mirage.Core.Monad

open System.Threading
open System.Threading.Tasks
open Cysharp.Threading.Tasks
open FSharpPlus
open FSharpx.Control
open FSharpPlus.Data

/// <summary>
/// Convert an <b>Async</b> to a <b>Task</b>.
/// </summary>
let toTask<'A> (token: CancellationToken) (program: Async<'A>) : Task<'A> =
    Async.StartImmediateAsTask(program, token)

/// <summary>
/// Convert an  <b>Async</b> to a <b>UniTask</b>.
/// </summary>
let toUniTask<'A> (token: CancellationToken) : Async<'A> -> UniTask<'A> =
    toTask token >> _.AsUniTask()

/// <summary>
/// Run the <b>Async</b> as a <b>UniTask</b>, but omit the return type.
/// </summary>
let toUniTask_ (token: CancellationToken) : Async<Unit> -> Unit =
    toUniTask token >> _.Forget()

/// <summary>
/// Run the given program from the async thread pool, and then return the value to the caller thread.
/// </summary>
let forkReturn<'A> (program: Async<'A>) : Async<'A> =
    async {
        let agent = new BlockingQueueAgent<'A>(1)
        Async.Start(agent.AsyncAdd =<< program)
        return! agent.AsyncGet()
    }

/// <summary>
/// Lift a <b>Result</b> into a <b>ResultT</b>.
/// </summary>
let inline liftResult (program: Result<'A, 'B>) : ResultT<'``Monad<Result<'A, 'B>>``> =
    ResultT <| result program