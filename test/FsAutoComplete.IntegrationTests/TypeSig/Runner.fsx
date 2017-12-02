#load "../TestHelpers.fsx"
open TestHelpers
open System.IO
open System

(*
 * This test is a simple sanity check of a basic run of the program.
 * A few completions, files and script.
 *)

Environment.CurrentDirectory <- __SOURCE_DIRECTORY__
File.Delete "output.json"

let p = new FsAutoCompleteWrapper()

p.parse "Script.fsx"
p.typesig "Script.fsx" "  let funky x = x + 1" 4 10
p.typesig "Script.fsx" "module CommandResponse =" 8 19
p.typesig "Script.fsx" "  type ResponseMsg<'T> =" 10 14
p.typesig "Script.fsx" "  let funky x = x + 1" 4 10
p.quit()
p.finalOutput ()
|> writeNormalizedOutput "output.json"

