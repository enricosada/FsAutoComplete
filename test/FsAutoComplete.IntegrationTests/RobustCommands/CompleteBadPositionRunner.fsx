#load "../TestHelpers.fsx"
open TestHelpers
open System.IO
open System

Environment.CurrentDirectory <- __SOURCE_DIRECTORY__
File.Delete "completebadposition.json"

let p = new FsAutoCompleteWrapper()

p.project "Project/Test1.fsproj"
p.parse "Project/Program.fs"
p.completion "Project/Program.fs" "whatever" 50 1
p.completion "Project/Program.fs" "module X =" 1 100
p.send "quit\n"
p.finalOutput ()
|> writeNormalizedOutput "completebadposition.json"

