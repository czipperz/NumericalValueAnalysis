# Numerical Value Analysis

This program takes C# source code and performs numerical value analysis on it.

    int i = 3;
    int j = random();
    if (i < j - 2) {
        i = j;
    }
    // what values can i be?
    f(i);

On line 3, we know i ∈ {3} and j ∈ (-∞, ∞).  So if `i < j - 2` then `i
< ∞ - 2 = ∞` and `j > i + 2 ⇒ j > 5`.  So on line 4 we have learned
that `j ∈ (5, ∞)`.  Thus `i = j` implies `i ∈ (5, ∞)`.  Once the if
statement ends, on line 7, `i ∈ {3} ∪ (5, ∞)`.  So `f` is called with
a value in the set `{3} ∪ (5, ∞)`.

## Applications

The point of this analysis is mostly to detect dead code.  So once we
know the range of values each variable can be, we just detect if there
is no possible way the if statement could be true or false.  If so
then we have detected dead code.

## Setup
