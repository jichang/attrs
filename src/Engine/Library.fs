module Feblr.Attrs.Engine

open Orleans
open System.Threading.Tasks
open FSharp.Control.Tasks.V2.ContextInsensitive

type Flag =
    { key: string
      value: bool }

type Prop =
    { key: string
      value: string }

type User =
    { id: int64
      flags: Map<string, bool>
      props: Map<string, string> }

type App =
    { id: int64
      name: string
      status: int }

type Condition =
    | Const of bool
    | Postfix of int * int64
    | PostfixRange of int * int64 * int64
    | Either of Condition * Condition
    | Both of Condition * Condition
    | All of Condition array

    member this.Check(userId) =
        match this with
        | Const value -> value
        | Postfix(digits, lastDigits) -> (userId % pown 10L digits) = lastDigits
        | PostfixRange(digits, lowerLimit, upperLimit) ->
            let lastDigits = (userId % pown 10L digits)
            lastDigits >= lowerLimit && lastDigits < upperLimit
        | Either(cond1, cond2) -> cond1.Check userId || cond2.Check userId
        | Both(cond1, cond2) -> cond1.Check userId && cond2.Check userId
        | All conditions ->
            Array.fold (fun (value: bool) (condition: Condition) -> value && condition.Check userId) true conditions

type Rule =
    { value: bool
      condition: Condition }

    member this.Check(userId: int64) =
        if this.condition.Check userId then this.value
        else false

type Feature =
    { id: int64
      key: string
      value: bool
      rules: Rule list }

    member this.Check(userId: int64) =
        List.fold (fun (value: bool) (rule: Rule) -> value && rule.Check userId) false this.rules


type IUserGrain =
    inherit Orleans.IGrainWithIntegerKey

    abstract Query: unit -> Task<User>
    abstract UpdateFlag: string -> bool -> Task<bool>
    abstract QueryFlag: string -> Task<bool option>
    abstract UpdateProp: string -> string -> Task<bool>
    abstract QueryProp: string -> Task<string option>

type UserGrain(id: int64) =
    inherit Grain()

    let mutable flags = Map.empty
    let mutable props = Map.empty

    interface IUserGrain with

        member __.Query(): Task<User> =
            task {
                let user =
                    { id = id
                      flags = flags
                      props = props }
                return user
            }

        member __.UpdateFlag (key: string) (value: bool) =
            task {
                flags <- Map.add key value flags
                return true
            }

        member __.QueryFlag(key: string): Task<bool option> = task { return Map.tryFind key flags }

        member __.UpdateProp (key: string) (value: string) =
            task {
                props <- Map.add key value props
                return true
            }

        member __.QueryProp(key: string): Task<string option> = task { return Map.tryFind key props }

type IAppGrain =
    inherit Orleans.IGrainWithIntegerKey

    abstract QueryFeatures: unit -> Task<Feature list>
    abstract AddFeature: Feature -> Task<bool>
    abstract DelFeature: int64 -> Task<bool>
    abstract CheckFeature: int64 -> int64 -> Task<bool>

    abstract AddUser: int64 -> Task<bool>
    abstract QueryUser: int64 -> Task<User option>
    abstract DelUser: int64 -> Task<bool>

type AppGrain() =
    inherit Grain()

    let mutable userIds: int64 list = List.empty
    let mutable features: Feature list = List.empty

    interface IAppGrain with

        member __.QueryFeatures() = task { return features }

        member __.AddFeature feature =
            task {
                match List.tryFind (fun _feature -> _feature.key = feature.key) features with
                | Some _ -> return false
                | None ->
                    features <- List.append [ feature ] features
                    return true
            }

        member __.DelFeature featureId =
            task {
                match List.tryFind (fun feature -> feature.id = featureId) features with
                | Some _ -> return false
                | None ->
                    features <- List.filter (fun feature -> feature.id <> featureId) features
                    return true
            }

        member __.CheckFeature featureId userId =
            task {
                match List.tryFind (fun feature -> feature.id = featureId) features with
                | Some feature -> return feature.Check userId
                | None -> return false
            }

        member __.AddUser userId =
            task {
                match List.tryFind (fun _userId -> _userId = userId) userIds with
                | Some _ -> return false
                | None ->
                    userIds <- List.append [ userId ] userIds
                    return true
            }

        member this.QueryUser userId =
            let trainFactory = this.GrainFactory
            task {
                match List.tryFind (fun _userId -> _userId = userId) userIds with
                | Some _ ->
                    let userGrain = trainFactory.GetGrain<IUserGrain>(userId)
                    let! user = userGrain.Query()
                    return Some user
                | None -> return None
            }

        member __.DelUser userId =
            task {
                userIds <- List.filter (fun _userId -> _userId <> userId) userIds
                return true
            }

type IClientGrain =
    inherit Orleans.IGrainWithIntegerKey

    abstract QueryApps: unit -> Task<App list>
    abstract AddApp: App -> Task<bool>
    abstract DelApp: int64 -> Task<bool>

type ClientGrain() =
    inherit Grain()

    let mutable apps: App list = List.empty

    interface IClientGrain with

        member __.QueryApps() = task { return apps }

        member __.AddApp app =
            task {
                match List.tryFind (fun (_app: App) -> _app.id = app.id) apps with
                | Some _ -> return false
                | None ->
                    apps <- List.append [ app ] apps
                    return true
            }

        member __.DelApp appId =
            task {
                match List.tryFind (fun (_app: App) -> _app.id = appId) apps with
                | Some _ ->
                    apps <- List.filter (fun _app -> _app.id <> appId) apps
                    return true
                | None -> return false
            }
