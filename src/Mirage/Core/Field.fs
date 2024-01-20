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
module Mirage.Core.Field

open FSharpPlus

/// <summary>
/// A convenience type for class fields.
/// </summary>
type Field<'A> = Ref<Option<'A>>

/// <summary>
/// A convenience type to make it simpler to create field getters.
/// </summary>
type Getter<'A> = ref<Option<'A>> -> string -> string -> Result<'A, string>

/// <summary>
/// Create a getter for an optional field, providing an error message if retrieving the value fails.
/// </summary>
let inline getter<'A> (className: string) (field: ref<Option<'A>>) (fieldName: string) (methodName: string) : Result<'A, string> =
    Option.toResultWith
        $"{className}#{methodName} was called while {fieldName} has not been initialized yet."
        field.Value

/// <summary>
/// Set the value of a field.
/// </summary>
let inline set<'A> (field: Field<'A>) (value: 'A) =
    field.Value <- Some value

/// <summary>
/// Set the value of a field, whose type is nullable.
/// </summary>
let inline setNullable (field: Field<'A>) (value: 'A) =
    field.Value <- Option.ofObj value

/// <summary>
/// Set the value of a field.
/// </summary>
let inline setOption (field: Field<'A>) (value: Option<'A>) =
    field.Value <- value

/// <summary>
/// Set the field's value to <b>None</b>.
/// </summary>
let inline setNone (field: Field<'A>) =
    field.Value <- None