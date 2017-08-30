﻿namespace FsAutoComplete

open System
open Newtonsoft.Json
open Newtonsoft.Json.Converters
open Microsoft.FSharp.Compiler
open Microsoft.FSharp.Compiler.SourceCodeServices

module private JsonSerializerConverters =

  let fsharpErrorSeverityWriter (writer: JsonWriter) value (serializer : JsonSerializer) =
    let s =
        match value with
        | FSharpErrorSeverity.Error -> "Error"
        | FSharpErrorSeverity.Warning -> "Warning"
    serializer.Serialize(writer, s)

  let workspacePeekFoundWriter (writer: JsonWriter) value (serializer : JsonSerializer) =
    let t, v =
        match value with
        | CommandResponse.WorkspacePeekFound.Directory d ->
            "directory", box d
        | CommandResponse.WorkspacePeekFound.Solution sln ->
            "solution", box sln
    writer.WriteStartObject()
    writer.WritePropertyName("Type")
    writer.WriteValue(t)
    writer.WritePropertyName("Data")
    serializer.Serialize(writer, v)
    writer.WriteEndObject()

  let workspacePeekFoundSolutionItemKindWriter (writer: JsonWriter) value (serializer : JsonSerializer) =
    let t, v =
        match value with
        | CommandResponse.WorkspacePeekFoundSolutionItemKind.Folder d ->
            "folder", box d
        | CommandResponse.WorkspacePeekFoundSolutionItemKind.MsbuildFormat msbuildProj ->
            "msbuildFormat", box msbuildProj
    writer.WriteStartObject()
    writer.WritePropertyName("Kind")
    writer.WriteValue(t)
    writer.WritePropertyName("Data")
    serializer.Serialize(writer, v)
    writer.WriteEndObject()

  let rangeWriter (writer: JsonWriter) (range: Range.range) (_serializer : JsonSerializer) =
    writer.WriteStartObject()
    writer.WritePropertyName("StartColumn")
    writer.WriteValue(range.StartColumn + 1)
    writer.WritePropertyName("StartLine")
    writer.WriteValue(range.StartLine)
    writer.WritePropertyName("EndColumn")
    writer.WriteValue(range.EndColumn + 1)
    writer.WritePropertyName("EndLine")
    writer.WriteValue(range.EndLine)
    writer.WriteEndObject()

  let projectSdkTypeWriter (writer: JsonWriter) value (serializer : JsonSerializer) =
    let t, v =
        match value with
        | CommandResponse.ProjectResponseInfo.Verbose ->
            "verbose", (Object())
        | CommandResponse.ProjectResponseInfo.ProjectJson ->
            "project.json", (Object())
        | CommandResponse.ProjectResponseInfo.DotnetSdk d->
            "dotnet/sdk", box d
    writer.WriteStartObject()
    writer.WritePropertyName("SdkType")
    writer.WriteValue(t)
    writer.WritePropertyName("Data")
    serializer.Serialize(writer, v)
    writer.WriteEndObject()

  let projectOutputTypeWriter (writer: JsonWriter) value (serializer : JsonSerializer) =
    let s =
        match value with
        | ProjectOutputType.Library -> "lib"
        | ProjectOutputType.Exe -> "exe"
        | ProjectOutputType.Custom(x) -> x.ToLower()
    serializer.Serialize(writer, s)

  let internal jsonConverters =

    let writeOnlyConverter (f: JsonWriter -> 'T -> JsonSerializer -> unit) (canConvert: Type -> Type -> bool) =
        { new JsonConverter() with
            member x.CanConvert(t:System.Type) = canConvert typeof<'T> t

            member x.WriteJson(writer, value, serializer) = f writer (value :?> 'T) serializer

            member x.ReadJson(_reader, _t, _, _serializer) =
              raise (System.NotSupportedException())

            member x.CanRead = false
            member x.CanWrite = true }

    let sameDU ty t = Microsoft.FSharp.Reflection.FSharpType.IsUnion(t) && t.BaseType = ty
    [| writeOnlyConverter fsharpErrorSeverityWriter (=)
       writeOnlyConverter rangeWriter (=)
       writeOnlyConverter workspacePeekFoundWriter sameDU
       writeOnlyConverter workspacePeekFoundSolutionItemKindWriter sameDU
       writeOnlyConverter projectSdkTypeWriter sameDU
       writeOnlyConverter projectOutputTypeWriter sameDU |]

module JsonSerializer =

  let internal writeJson(o: obj) = JsonConvert.SerializeObject(o, JsonSerializerConverters.jsonConverters)
