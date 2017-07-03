open System
open System.IO
open System.Diagnostics
open System.Text.RegularExpressions

#I "../../packages/Newtonsoft.Json/lib/net45/"
#r "Newtonsoft.Json.dll"
open Newtonsoft.Json

let (</>) a b = Path.Combine(a,b)

type FsAutoCompleteWrapper() =

  let p = new System.Diagnostics.Process()
  let cachedOutput = new Text.StringBuilder()

  do
    p.StartInfo.FileName <-
      IO.Path.Combine(__SOURCE_DIRECTORY__,
                      "../../src/FsAutoComplete/bin/Debug/fsautocomplete.exe")
    p.StartInfo.RedirectStandardOutput <- true
    p.StartInfo.RedirectStandardError  <- true
    p.StartInfo.RedirectStandardInput  <- true
    p.StartInfo.UseShellExecute <- false
    p.StartInfo.EnvironmentVariables.Add("FCS_ToolTipSpinWaitTime", "10000")
    if Environment.GetEnvironmentVariable("FSAC_TESTSUITE_WAITDEBUGGER") = "1" then
      p.StartInfo.Arguments <- "--wait-for-debugger"
    p.Start () |> ignore

  member x.project (s: string) : unit =
    fprintf p.StandardInput "project \"%s\"\n" s

  member x.parse (s: string) : unit =
    let text = if IO.File.Exists s then IO.File.ReadAllText(s) else ""
    fprintf p.StandardInput "parse \"%s\" sync\n%s\n<<EOF>>\n" s text

  member x.parseContent (filename: string) (content: string) : unit =
    fprintf p.StandardInput "parse \"%s\" sync\n%s\n<<EOF>>\n" filename content

  member x.completion (fn: string) (lineStr:string)(line: int) (col: int) : unit =
    fprintf p.StandardInput "completion \"%s\" \"%s\" %d %d\n" fn lineStr line col

  member x.methods (fn: string) (lineStr: string)(line: int) (col: int) : unit =
    fprintf p.StandardInput "methods \"%s\" \"%s\" %d %d\n" fn lineStr line col

  member x.completionFilter (fn: string) (lineStr: string)(line: int) (col: int) (filter: string) : unit =
    fprintf p.StandardInput "completion \"%s\" \"%s\" %d %d filter=%s\n" fn lineStr line col filter

  member x.tooltip (fn: string) (lineStr: string) (line: int) (col: int) : unit =
    fprintf p.StandardInput "tooltip \"%s\" \"%s\" %d %d\n" fn lineStr line col

  member x.typesig (fn: string) (lineStr: string) (line: int) (col: int) : unit =
    fprintf p.StandardInput "typesig \"%s\" \"%s\" %d %d\n" fn lineStr line col

  member x.finddeclaration (fn: string) (lineStr: string) (line: int) (col: int) : unit =
    fprintf p.StandardInput "finddecl \"%s\" \"%s\" %d %d\n" fn lineStr line col

  member x.symboluse (fn: string) (lineStr: string) (line: int) (col: int) : unit =
    fprintf p.StandardInput "symboluse \"%s\" \"%s\" %d %d\n" fn lineStr line col

  member x.declarations (fn: string) : unit =
    fprintf p.StandardInput "declarations \"%s\"\n" fn

  member x.lint (fn: string) : unit =
    fprintf p.StandardInput "lint \"%s\"\n" fn

  member x.send (s: string) : unit =
    fprintf p.StandardInput "%s" s

  /// Wait for a single line to be output (one JSON message)
  /// Note that this line will appear at the *start* of output.json,
  /// so use carefully, and preferably only at the beginning.
  member x.waitForLine () : unit =
    cachedOutput.AppendLine(p.StandardOutput.ReadLine()) |> ignore

  member x.finalOutput () : string =
    let s = p.StandardOutput.ReadToEnd()
    let t = p.StandardError.ReadToEnd()
    p.WaitForExit()
    cachedOutput.ToString() + s + t

let formatJson json =
    try
      let parsedJson = JsonConvert.DeserializeObject(json)
      JsonConvert.SerializeObject(parsedJson, Formatting.Indented)
    with _ -> json

let writeNormalizedOutput (fn: string) (s: string) =
  let lines = s.TrimEnd().Split('\n')
  for i in [ 0 .. lines.Length - 1 ] do
    if Path.GetExtension fn = ".json" then
      lines.[i] <- formatJson lines.[i]

    if Path.DirectorySeparatorChar = '/' then
      lines.[i] <- Regex.Replace(lines.[i],
                                 "/.*?test/FsAutoComplete\.IntegrationTests/(.*?(\"|$))",
                                 "<absolute path removed>/FsAutoComplete.IntegrationTests/$1")
      lines.[i] <- Regex.Replace(lines.[i],
                                 "\"/[^\"]*?/([^\"/]*?\.dll\")",
                                  "\"<absolute path removed>/$1")
    else
      if Path.GetExtension fn = ".json" then
        lines.[i] <- Regex.Replace(lines.[i].Replace(@"\\", "/"),
                                   "[a-zA-Z]:/.*?test/FsAutoComplete\.IntegrationTests/(.*?(\"|$))",
                                   "<absolute path removed>/FsAutoComplete.IntegrationTests/$1")
        lines.[i] <- Regex.Replace(lines.[i],
                                   "\"[a-zA-Z]:/[^\"]*?/([^\"/]*?\.dll\")",
                                   "\"<absolute path removed>/$1")
      else
        lines.[i] <- Regex.Replace(lines.[i].Replace('\\','/'),
                                   "[a-zA-Z]:/.*?test/FsAutoComplete\.IntegrationTests/(.*?(\"|$))",
                                   "<absolute path removed>/FsAutoComplete.IntegrationTests/$1")


    lines.[i] <- lines.[i].Replace("\r", "").Replace(@"\r", "")

  // Write manually to ensure \n line endings on all platforms
  using (new StreamWriter(fn))
  <| fun f ->
      for line in lines do
        f.Write(line)
        f.Write('\n')

let runProcess (workingDir: string) (exePath: string) (args: string) =
    printfn "Running '%s %s' in working dir '%s'" exePath args workingDir
    let psi = System.Diagnostics.ProcessStartInfo()
    psi.FileName <- exePath
    psi.WorkingDirectory <- workingDir 
    psi.RedirectStandardOutput <- false
    psi.RedirectStandardError <- false
    psi.Arguments <- args
    psi.CreateNoWindow <- true
    psi.UseShellExecute <- false

    use p = new System.Diagnostics.Process()
    p.StartInfo <- psi
    p.Start() |> ignore
    p.WaitForExit()
      
    let exitCode = p.ExitCode
    exitCode

let runProcessCaptureOut (workingDir: string) (exePath: string) (args: string) =
    printfn "Running '%s %s' in working dir '%s'" exePath args workingDir
    let psi = System.Diagnostics.ProcessStartInfo()
    psi.FileName <- exePath
    psi.WorkingDirectory <- workingDir
    psi.RedirectStandardOutput <- true
    psi.RedirectStandardError <- true
    psi.Arguments <- args
    psi.CreateNoWindow <- true
    psi.UseShellExecute <- false

    use p = new System.Diagnostics.Process()
    p.StartInfo <- psi

    let sbOut = System.Collections.Generic.List<string>()
    p.OutputDataReceived.Add(fun ea ->
        printfn "%s" (ea.Data)
        sbOut.Add(ea.Data) |> ignore)

    let sbErr = System.Collections.Generic.List<string>()
    p.ErrorDataReceived.Add(fun ea ->
        printfn "%s" (ea.Data)
        sbErr.Add(ea.Data) |> ignore)

    p.Start() |> ignore
    p.BeginOutputReadLine()
    p.BeginErrorReadLine()
    p.WaitForExit()

    let exitCode = p.ExitCode
    (exitCode, sbOut |> List.ofSeq, sbErr |> List.ofSeq)

let processResultLog msg (err, outData, errData) =
    let sb = System.Text.StringBuilder()
    sb.Append(sprintf "%s with exit code %i" msg err) |> ignore
    sb.Append("Output:") |> ignore
    outData |> List.iter (fun (s: string) -> sb.Append(s) |> ignore)
    sb.Append("Error:") |> ignore
    errData |> List.iter (fun (s: string) -> sb.Append(s) |> ignore)
    sb.ToString()

let (|NonExitCodeResult|_|) processResult =
  match processResult with
  | (0,_,_) -> None
  | data -> Some data

let deleteDir d = 
  if Directory.Exists(d) then
    printfn "Deleting dir '%s'" d
    Directory.Delete(d, true)

let setEnvVar envVar f =
  let oldValue = System.Environment.GetEnvironmentVariable(envVar)
  let newValue = f oldValue
  System.Environment.SetEnvironmentVariable(envVar, newValue)

  { new IDisposable with 
    member x.Dispose() =
      System.Environment.SetEnvironmentVariable(envVar, oldValue) }

let withPath dir =
  setEnvVar "PATH" (fun pathvar -> dir + Path.PathSeparator.ToString() + pathvar)

module DotnetCli =
  let sdk2Dir () =
    let isWindows = Environment.OSVersion.Platform = PlatformID.Win32NT
    let file = if isWindows then "dotnet-install.ps1" else "dotnet-install.sh"
    let repoDir = Path.Combine(__SOURCE_DIRECTORY__, "..", "..")
    let sdkDir = Path.Combine(repoDir, ".dotnetsdk2_0") |> Path.GetFullPath

    if Directory.Exists(sdkDir) then
      printfn ".net core sdk found in '%s'" sdkDir
      sdkDir
    else
      printfn ".net core sdk not found in '%s'" sdkDir

      Directory.CreateDirectory(sdkDir) |> ignore

      let branch = "release/2.0.0-preview2"
    
      use client = new System.Net.WebClient()
      let installScriptPath = Path.Combine(sdkDir, file)
      let installScriptUrl = sprintf "https://raw.githubusercontent.com/dotnet/cli/%s/scripts/obtain/%s" branch file
      printfn "downloading .net core sdk install script %s" installScriptUrl
      try
        client.DownloadFile(installScriptUrl, installScriptPath)
      with _ when not(isWindows) ->
        //DownloadFile fails in WLS (https://github.com/Microsoft/BashOnWindows/issues/1639), fallback to curl
        printfn "download failed, retry with curl"
        match runProcess __SOURCE_DIRECTORY__  "curl" (sprintf "%s -o %s" installScriptUrl installScriptPath) with
        | 0 -> ()
        | _ -> failwithf "Failed to download script '%s' from curl" installScriptUrl

      printfn "installing .net core sdk (branch %s) to '%s'" branch sdkDir

      if isWindows then
        let powershell script args = runProcess __SOURCE_DIRECTORY__ "powershell" (sprintf """-NoProfile -ExecutionPolicy unrestricted -File "%s" %s """ script args) |> ignore
        powershell installScriptPath  (sprintf "-InstallDir %s -Channel %s" sdkDir branch)
      else
        let bash script args = runProcess __SOURCE_DIRECTORY__ "bash" (sprintf """ "%s" %s """ script args) |> ignore
        bash installScriptPath  (sprintf "--install-dir %s -channel %s" sdkDir branch)

      sdkDir

  let useSdk sdkDir =
    let p = withPath sdkDir
    let e = setEnvVar "DOTNET_SKIP_FIRST_TIME_EXPERIENCE" (fun _ -> "1")
    { new IDisposable with
      member x.Dispose() = p.Dispose(); e.Dispose() }

  let withNetFxBclAvaiable version =
    let isWindows = Environment.OSVersion.Platform = PlatformID.Win32NT
    if isWindows then
      //on windows is not needed
      { new IDisposable with member x.Dispose() = () }
    else
      let monoLibPath = "/usr/lib/mono/"
      setEnvVar "FrameworkPathOverride" (fun _ -> sprintf "%s%s-api/" monoLibPath version)
