namespace Feblr.Attrs.Engine

open Orleans
open System.Threading.Tasks

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
    { key: string
      value: bool
      rules: Rule list }

    member this.Check(userId: int64) =
        List.fold (fun (value: bool) (rule: Rule) -> value && rule.Check userId) false this.rules


type IUserGrain =
    inherit Orleans.IGrainWithGuidCompoundKey

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
            Task.FromResult
                { id = id
                  flags = flags
                  props = props }


        member __.UpdateFlag (key: string) (value: bool): Task<bool> =
            flags <- Map.add key value flags
            Task.FromResult true

        member __.QueryFlag(key: string): Task<bool option> = Map.tryFind key flags |> Task.FromResult

        member __.UpdateProp (key: string) (value: string): Task<bool> =
            props <- Map.add key value props
            Task.FromResult true

        member __.QueryProp(key: string): Task<string option> = Map.tryFind key props |> Task.FromResult

type IAppGrain =
    inherit Orleans.IGrainWithGuidKey

    abstract AddFeature: Feature -> Task<bool>
    abstract DelFeature: Feature -> Task<bool>
    abstract CheckFeature: int64 -> string -> Task<bool>

    abstract AddUser: int64 -> Task<bool>
    abstract DelUser: int64 -> Task<bool>

type AppGrain() =
    inherit Grain()

    let mutable userIds: int64 list = List.empty
    let mutable features: Feature list = List.empty

    interface IAppGrain with

        member __.AddFeature feature =
            match List.tryFind (fun _feature -> _feature.key = feature.key) features with
            | Some _ -> Task.FromResult false
            | None ->
                features <- List.append [ feature ] features
                Task.FromResult true

        member __.DelFeature feature =
            match List.tryFind (fun _feature -> _feature.key = feature.key) features with
            | Some _ -> Task.FromResult false
            | None ->
                features <- List.append [ feature ] features
                Task.FromResult true

        member __.CheckFeature userId featureKey =
            match List.tryFind (fun feature -> feature.key = featureKey) features with
            | Some feature -> feature.Check userId |> Task.FromResult
            | None -> Task.FromResult false

        member __.AddUser userId =
            match List.tryFind (fun _userId -> _userId = userId) userIds with
            | Some _ -> Task.FromResult false
            | None ->
                userIds <- List.append [ userId ] userIds
                Task.FromResult true

        member __.DelUser userId =
            userIds <- List.filter (fun _userId -> _userId <> userId) userIds
            Task.FromResult true

type IClientGrain =
    inherit Orleans.IGrainWithGuidKey

    abstract QueryApps: unit -> Task<App list>
    abstract AddApp: App -> Task<bool>
    abstract DelApp: App -> Task<bool>

type ClientGrain() =
    inherit Grain()

    let mutable apps: App list = List.empty

    interface IClientGrain with

        member __.QueryApps() = Task.FromResult apps

        member __.AddApp app =
            match List.tryFind (fun (_app: App) -> _app.id = app.id) apps with
            | Some _ -> Task.FromResult false
            | None ->
                apps <- List.append [ app ] apps
                Task.FromResult true

        member __.DelApp app =
            match List.tryFind (fun (_app: App) -> _app.id = app.id) apps with
            | Some _ ->
                apps <- List.filter (fun _app -> _app.id <> app.id) apps
                Task.FromResult true
            | None -> Task.FromResult false
