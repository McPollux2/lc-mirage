module Mirage.Test.Assertion

open NUnit.Framework

let assertEquals<'A> (expected: 'A) (actual: 'A)  = Assert.AreEqual(expected, actual)
let assertTrue condition errorMessage = Assert.IsTrue(condition, errorMessage)