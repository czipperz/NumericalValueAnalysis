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
### C# Frontend

To setup this project, simply clone
[it](https://github.com/czipperz/NumericalValueAnalysis), then load it
into Visual Studio.

### Rust Backend

1. To setup the backend, clone
[it](https://github.com/czipperz/numerical_value).

2. If you haven't yet, set up
[Rust](https://www.rust-lang.org/en-US/install.html).

3. Inside the `numerical_value` directory, run `cargo build --release`

4. Copy `target/release/numerical_value.exe` to `../HelloRoslyn/bin/Debug/`

## Usage
To change the input program edit the string at line 43 of Program.cs
and then run it.  This will output diagnostics about the code.  Set
based diagnostics can be accessed by running numerical_value directly
(run inside the numerical_value directory):

    cargo run input.json diagnostics.json

input.json should be filled with json output from Visual Studio
command line (see Builder:).  Do not use quotes.

diagnostics.json will be the output diagnostics.

Command line out will be filled with set information.
