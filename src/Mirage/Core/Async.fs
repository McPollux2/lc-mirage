module Mirage.Core.Async

open System.Threading
open System.Threading.Tasks
open Cysharp.Threading.Tasks
open FSharpPlus
open FSharpx.Control

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
/// Run the given program from the async thread pool, and then returns
/// the value to the caller thread.
/// </summary>
let forkReturn<'A> (program: Async<'A>) : Async<'A> =
    async {
        let mvar = new BlockingQueueAgent<'A>(1)
        Async.Start(mvar.AsyncAdd =<< program)
        return! mvar.AsyncGet()
    }