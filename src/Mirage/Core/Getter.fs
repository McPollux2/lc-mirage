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
module Mirage.Core.Getter

open FSharpPlus

/// <summary>
/// A convenience type to make it simpler to create field getters.
/// </summary>
type Getter<'A> = ref<Option<'A>> -> string -> string -> Result<'A, string>

/// <summary>
/// Create a getter for an optional field, providing an error message if retrieving the value fails.
/// </summary>
let getter<'A> (className: string) (field: ref<Option<'A>>) (fieldName: string) (methodName: string) : Result<'A, string> =
    Option.toResultWith
        $"{className}#{methodName} was called while {fieldName} has not been initialized yet."
        field.Value

/// <summary>
/// A convenience type for class fields that use <b>Getter</b>.
/// </summary>
type Field<'A> = Ref<Option<'A>>