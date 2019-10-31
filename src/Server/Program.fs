open System
open System.IO
open Microsoft.Extensions.Hosting
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.DependencyInjection
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Logging
open FSharp.Control.Tasks.V2.ContextInsensitive

open Giraffe
open Feblr.Attrs
open Feblr.Attrs.OAuth
open Hopac

open Orleans
open Orleans.Hosting
open Feblr.Attrs.Engine

module View =
    open GiraffeViewEngine

    let layout (content: XmlNode list) =
        html [] [
            head [] [
                title []  [ encodedText "Feblr Attrs" ]
                link [ _rel  "stylesheet"
                       _type "text/css"
                       _href "/styles/main.css" ]
            ]
            body [] content
        ]

    let signinView (oauthCfg: OAuthConfig) =
        let link = sprintf "%s?client_id=%s&server_id=%s&scope_name=%s&redirect_uri=%s&response_type=code&state=%s" oauthCfg.endpoint oauthCfg.clientId oauthCfg.serverId oauthCfg.scopeName oauthCfg.redirectUri oauthCfg.state
        let linkBtn = a [ _href link ] [ encodedText "Sign In"]
        [ div [ _class "form--signin text-align--center container" ] [ h2 [] [ encodedText "Welcome to Feblr Attrs" ]; linkBtn ]]
        |> layout

    let importLink (oauthCfg: OAuthConfig) : string = 
        sprintf "%s?client_id=%s&server_id=%s&scope_name=%s&redirect_uri=%s&response_type=code&state=%s" oauthCfg.endpoint oauthCfg.clientId oauthCfg.serverId oauthCfg.scopeName oauthCfg.redirectUri oauthCfg.state

    let appRow (app: App) =
        let linkBtn = a [_href (sprintf "/apps/%d" app.id) ] [encodedText "Details"]
        tr []
            [ td [ _class "text-align--right" ] [ encodedText (app.id.ToString()) ]
              td [] [ encodedText app.name ] 
              td [ _class "text-align--right" ] [ linkBtn ] ]

    let appsTable (apps: App list) =
        let tableHeader =
            thead []
                [ tr [] [ td [ _class "text-align--right" ] [ encodedText "Id" ]; td [ _class "text-align--right" ] [ encodedText "Name" ]; td [] [] ] ]

        let tableBody =
            tbody [] (List.map appRow apps)

        table [] [ tableHeader ; tableBody ]

    let headerView (oauthCfg: OAuthConfig) =
        let link = importLink { oauthCfg with scopeName = "user.apps"; state = "user.apps" }
        let title = h3 [ _class "flex__item" ] [ encodedText "Feblr Attrs "]
        let importBtn = a [ _href link ] [ encodedText "Import Apps" ]
        header [] [ div [ _class "container" ] [ div [_class "flex__box"] [title; importBtn] ] ]

    let indexView (oauthCfg: OAuthConfig) (apps: App list) =
        [ headerView oauthCfg; div [ _class "container" ] [appsTable apps] ]
        |> layout

    let appView (app: App) (users: User seq) =
        []
        |> layout

module Handler =
    let index (oauthCfg: OAuthConfig) =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            task {
                let openId = ctx.Session.GetString "open_id"
                match openId with
                | null ->
                    let view = View.signinView { oauthCfg with scopeName = "user.id"; state = "user.id" }
                    return! htmlView view next ctx
                | _ ->
                    let clusterClient = ctx.GetService<IClusterClient>()
                    let openId = Guid.Parse (openId)
                    let clientGrain = clusterClient.GetGrain<IClientGrain>(openId)
                    let! apps = clientGrain.QueryApps ()
                    let view = View.indexView { oauthCfg with scopeName = "user.apps"; state = "user.apps" } apps
                    return! htmlView view next ctx
            }

    let oauth (oauthCfg: OAuthConfig) : HttpHandler =
        fun (next : HttpFunc) (ctx : HttpContext) ->
            task {
                match ctx.TryGetQueryStringValue "code", ctx.TryGetQueryStringValue "state" with
                | None, _
                | _, None ->
                    return! redirectTo false "/code" next ctx
                | Some code, Some state ->
                    match state with
                    | "user.id" ->
                        let ticket =
                            OAuth.oauth code oauthCfg
                            |> run
                        match ticket with
                        | Ok ticket ->
                            ctx.Session.SetString("open_id", ticket.open_id)
                            return! redirectTo false "/" next ctx
                        | Error error ->
                            return! redirectTo false "/" next ctx
                    | "user.apps" ->
                        let ticket =
                            OAuth.oauth code oauthCfg
                            |> run
                        match ticket with
                        | Ok ticket ->
                            let queryResult =
                                OAuth.queryApps "http://sso.feblr.org/api/v1/applications" ticket
                                |> run
                            match queryResult with
                            | Ok apps ->
                                let clusterClient = ctx.GetService<IClusterClient>()
                                let openId = Guid.Parse (ticket.open_id)
                                let clientGrain = clusterClient.GetGrain<IClientGrain>(openId)
                                return! redirectTo false "/" next ctx
                            | _ ->
                                return! redirectTo false "/" next ctx
                        | Error error ->
                            return! redirectTo false "/" next ctx
                    | _ ->
                        return! redirectTo false "/" next ctx
            }

    let app (appId: int64) : HttpHandler =
        fun (next : HttpFunc) (ctx : HttpContext) ->
            task {
                let openIdStr = ctx.Session.GetString "open_id"
                let clusterClient = ctx.GetService<IClusterClient>()
                let openId = Guid.Parse (openIdStr)
                let appGrain = clusterClient.GetGrain<IAppGrain>(openId, appId.ToString())
                return! redirectTo false "/code" next ctx
            }

let webApp =
    let oauthCfg =
        { endpoint = "http://sso.feblr.org/oauth"
          serverId = "f9be1b35c13a394cd7fbda5e38be80fbf68d5c685e65ada2c41f9f46832cc8fbb889211e1e9723e3115e4adecd097ddab019515e02848f2d5d9fcfeb0cec3892"
          clientId = "1e9f7ef873b9141cd22e6f3e6e9e59295eaeb3f38594e0c8f449240dbc6ff90a20473baf3d3e96bbf55bc7517a5f99b7d6e0ab9aa3530fc1bc568db2c9e6027a"
          clientSecret = "0a07328bf2ab1121466878d304acd950b894f254cd91c0537ff986b0fe109b7b304ce96e39084e3eb54bdda8d026f05e0b5ca658a79d77b09fd891ebd49b1971271da6128250584da9bfd4a8273d19018bf86586fa7d3167f3afec05aae91d6f9911e1b64846c5c6937c36446113b070dced8e376d10d2dc2367e62ddc76bf0f"
          redirectUri = "http://attrs.feblr.org/oauth"
          scopeName = ""
          state = ""
          ticketEndpoint = "http://sso.feblr.org/api/v1/tickets" }
    choose [
        GET >=> route "/" >=> Handler.index oauthCfg
        GET >=> route "/oauth" >=> Handler.oauth oauthCfg
        GET >=> routef "/apps/%d" Handler.app ]

let configureApp (app : IApplicationBuilder) =
    app.UseStaticFiles() |> ignore
    app.UseSession() |> ignore
    app.UseGiraffe webApp

let configureServices (services : IServiceCollection) =
    services.AddDistributedMemoryCache() |> ignore
    services.AddSession (fun options ->
        options.IdleTimeout <- TimeSpan.FromSeconds (60.0 * 60.0 * 24.0)
        options.Cookie.HttpOnly <- true
        options.Cookie.IsEssential <- true
    ) |> ignore
    services.AddGiraffe() |> ignore

let configureLogging (builder : ILoggingBuilder) =
    // Set a logging filter (optional)
    let filter (l : LogLevel) = l.Equals LogLevel.Error

    // Configure the logging factory
    builder.AddConsole()      // Set up the Console logger
           .AddDebug()        // Set up the Debug logger

           // Add additional loggers if wanted...
    |> ignore


[<EntryPoint>]
let main _ =
    let host =
        HostBuilder()
            .ConfigureWebHostDefaults(fun webHostBuilder ->
                let contentRoot = Directory.GetCurrentDirectory()
                let webRoot     = Path.Combine(contentRoot, "WebRoot")
                webHostBuilder
                    .UseKestrel()
                    .UseContentRoot(contentRoot)
                    .UseWebRoot(webRoot)
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
