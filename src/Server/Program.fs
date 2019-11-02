open System
open Microsoft.Extensions.Hosting
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging

open Giraffe
open Thoth.Json.Net
open Thoth.Json.Giraffe

open Orleans
open Orleans.Hosting
open Feblr.Attrs.Engine
open Feblr.Attrs.Server.Handlers

open Giraffe.Serialization

let webApp =
    choose
        [ GET >=> route "/apps" >=> Client.queryApps
          POST >=> route "/apps" >=> Client.createApp
          DELETE >=> routef "/apps/%d" Client.deleteApp
          GET >=> routef "/apps/%d/features" App.queryFeatures
          POST >=> routef "/apps/%d/features" App.createFeature
          DELETE >=> routef "/apps/%d/features/%d" App.deleteFeature
          GET >=> routef "/apps/%d/features/%d/users/%d" App.checkFeature
          POST >=> routef "/apps/%d/users" App.createUser
          GET >=> routef "/apps/%d/users/%d" App.queryUser
          DELETE >=> routef "/apps/%d/users/%d" App.deleteUser
          GET >=> routef "/apps/%d/users/%d/flags" User.QueryFlag
          POST >=> routef "/apps/%d/users/%d/flags" User.UpdateFlag
          GET >=> routef "/apps/%d/users/%d/props" User.QueryProp
          POST >=> routef "/apps/%d/users/%d/props" User.UpdateProp ]

let configureApp (app : IApplicationBuilder) =
    app.UseGiraffe webApp

let configureServices (services : IServiceCollection) =
    services.AddGiraffe() |> ignore
    let extraCoders =
        Extra.empty
        |> Extra.withInt64
        |> Extra.withDecimal
    services.AddSingleton<IJsonSerializer>(ThothSerializer(extra=extraCoders)) |> ignore

let configureLogging (builder : ILoggingBuilder) =
    let filter (l : LogLevel) = l.Equals LogLevel.Error
    builder
        .AddFilter(filter)
        .AddConsole()
        .AddDebug()
    |> ignore


[<EntryPoint>]
let main _ =
    let host =
        HostBuilder()
            .ConfigureWebHostDefaults(fun webHostBuilder ->
                webHostBuilder
                    .UseKestrel()
                    .UseUrls("http://localhost:6060")
                    .Configure(Action<IApplicationBuilder> configureApp)
                    .ConfigureServices(configureServices)
                    .ConfigureLogging(configureLogging)
                    |> ignore
            )
            .UseOrleans(fun siloBuilder ->
                let assembly = typeof<IClientGrain>.Assembly
                siloBuilder
                    .UseLocalhostClustering()
                    .ConfigureApplicationParts(fun parts -> parts.AddApplicationPart(assembly).WithCodeGeneration() |> ignore)
                    |> ignore
            )
            .Build()

    host.Run()

    0
