module Feblr.Attrs.Server.Handlers

open Giraffe
open Microsoft.AspNetCore.Http
open FSharp.Control.Tasks.V2.ContextInsensitive

open Orleans
open Feblr.Attrs.Engine

module Client =
    let queryApps (next: HttpFunc) (ctx: HttpContext) =
        task {
            let clusterClient = ctx.GetService<IClusterClient>()
            let clientGrain = clusterClient.GetGrain<IClientGrain>(0L)
            let! apps = clientGrain.QueryApps()
            return! ctx.WriteJsonAsync apps
        }

    let createApp (next: HttpFunc) (ctx: HttpContext) =
        task {
            let! app = ctx.BindJsonAsync<App>()
            let clusterClient = ctx.GetService<IClusterClient>()
            let clientGrain = clusterClient.GetGrain<IClientGrain>(0L)
            let! result = clientGrain.AddApp app
            match result with
            | true -> return! Successful.created (json app) next ctx
            | false -> return! RequestErrors.conflict (json app) next ctx
        }

    let deleteApp (appId: int64): HttpHandler =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            task {
                let clusterClient = ctx.GetService<IClusterClient>()
                let clientGrain = clusterClient.GetGrain<IClientGrain>(0L)
                let! result = clientGrain.DelApp appId
                match result with
                | true -> return! Successful.ok (text "") next ctx
                | false -> return! RequestErrors.notFound (text "") next ctx
            }

module App =
    let queryFeatures (appId: int64): HttpHandler =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            task {
                let clusterClient = ctx.GetService<IClusterClient>()
                let appGrain = clusterClient.GetGrain<IAppGrain>(appId)
                let! features = appGrain.QueryFeatures()
                return! ctx.WriteJsonAsync features
            }

    let createFeature (appId: int64): HttpHandler =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            task {
                let! feature = ctx.BindJsonAsync<Feature>()
                let clusterClient = ctx.GetService<IClusterClient>()
                let appGrain = clusterClient.GetGrain<IAppGrain>(appId)
                let! result = appGrain.AddFeature feature
                match result with
                | true -> return! Successful.created (json feature) next ctx
                | false -> return! RequestErrors.conflict (json feature) next ctx
            }

    let deleteFeature (appId, featureId): HttpHandler =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            task {
                let clusterClient = ctx.GetService<IClusterClient>()
                let appGrain = clusterClient.GetGrain<IAppGrain>(appId)
                let! result = appGrain.DelFeature featureId
                match result with
                | true -> return! Successful.ok (text "") next ctx
                | false -> return! RequestErrors.notFound (text "") next ctx
            }

    let checkFeature (appId, featureId, userId): HttpHandler =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            task {
                let clusterClient = ctx.GetService<IClusterClient>()
                let appGrain = clusterClient.GetGrain<IAppGrain>(appId)
                let! result = appGrain.CheckFeature featureId userId
                match result with
                | true -> return! Successful.ok (text "") next ctx
                | false -> return! RequestErrors.notFound (text "") next ctx
            }

    let createUser (appId: int64): HttpHandler =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            task {
                let! userId = ctx.BindJsonAsync<int64>()
                let clusterClient = ctx.GetService<IClusterClient>()
                let appGrain = clusterClient.GetGrain<IAppGrain>(appId)
                let! result = appGrain.AddUser userId
                match result with
                | true -> return! Successful.created (text "") next ctx
                | false -> return! RequestErrors.conflict (text "") next ctx
            }

    let queryUser (appId, userId): HttpHandler =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            task {
                let clusterClient = ctx.GetService<IClusterClient>()
                let appGrain = clusterClient.GetGrain<IAppGrain>(appId)
                let! result = appGrain.QueryUser userId
                match result with
                | Some user -> return! Successful.created (json user) next ctx
                | None -> return! RequestErrors.notFound (text "") next ctx
            }

    let deleteUser (appId, userId): HttpHandler =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            task {
                let clusterClient = ctx.GetService<IClusterClient>()
                let appGrain = clusterClient.GetGrain<IAppGrain>(appId)
                let! result = appGrain.DelUser userId
                match result with
                | true -> return! Successful.ok (text "") next ctx
                | false -> return! RequestErrors.notFound (text "") next ctx
            }

module User =
    let UpdateFlag((appId, userId): int64 * int64): HttpHandler =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            task {
                let! flag = ctx.BindJsonAsync<Flag>()
                let clusterClient = ctx.GetService<IClusterClient>()
                let userGrain = clusterClient.GetGrain<IUserGrain>(userId)
                let! result = userGrain.UpdateFlag flag.key flag.value
                match result with
                | true -> return! Successful.ok (text "") next ctx
                | false -> return! RequestErrors.notFound (text "") next ctx
            }

    let QueryFlag((appId, userId): int64 * int64): HttpHandler =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            task {
                match ctx.GetQueryStringValue "flag" with
                | Ok flagKey ->
                    let clusterClient = ctx.GetService<IClusterClient>()
                    let userGrain = clusterClient.GetGrain<IUserGrain>(userId)
                    let! result = userGrain.QueryFlag flagKey
                    match result with
                    | Some value ->
                        let flag: Flag =
                            { key = flagKey
                              value = value }
                        return! Successful.ok (json flag) next ctx
                    | None -> return! RequestErrors.notFound (text "") next ctx
                | Error _ -> return! RequestErrors.badRequest (text "") next ctx
            }

    let UpdateProp((appId, userId): int64 * int64): HttpHandler =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            task {
                let! prop = ctx.BindJsonAsync<Prop>()
                let clusterClient = ctx.GetService<IClusterClient>()
                let userGrain = clusterClient.GetGrain<IUserGrain>(userId)
                let! result = userGrain.UpdateProp prop.key prop.value
                match result with
                | true -> return! Successful.ok (text "") next ctx
                | false -> return! RequestErrors.notFound (text "") next ctx
            }

    let QueryProp((appId, userId): int64 * int64): HttpHandler =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            task {
                match ctx.GetQueryStringValue "prop" with
                | Ok propKey ->
                    let clusterClient = ctx.GetService<IClusterClient>()
                    let userGrain = clusterClient.GetGrain<IUserGrain>(userId)
                    let! result = userGrain.QueryProp propKey
                    match result with
                    | Some value ->
                        let prop: Prop =
                            { key = propKey
                              value = value }
                        return! Successful.ok (json prop) next ctx
                    | None -> return! RequestErrors.notFound (text "") next ctx
                | Error _ -> return! RequestErrors.badRequest (text "") next ctx
            }
