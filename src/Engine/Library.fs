namespace Feblr.Attrs.Engine

open System
open Orleans
open Orleans.Runtime.Configuration
open Orleans.Hosting
open System.Threading.Tasks
open FSharp.Control.Tasks.V2

module Host =
    type User =
        { id: int64
          flags: Map<string, bool>
          props: Map<string, string> }

    type IUserGrain = 
        inherit Orleans.IGrainWithGuidKey
        abstract member Query : unit -> Task<User>
        abstract member UpdateFlag : string -> bool -> Task<bool>
        abstract member UpdateProp : string -> string -> Task<bool>

    type UserGrain(id: int64) =
        inherit Grain ()

        let mutable flags = Map.empty
        let mutable props = Map.empty

        interface IUserGrain with
            member __.Query () : Task<User> =
                Task.FromResult { id = id; flags = flags; props = props }

            member __.UpdateFlag (key : string) (value: bool) : Task<bool> =
                flags <- Map.add key value flags
                Task.FromResult true

            member __.UpdateProp (key : string) (value: string) : Task<bool> = 
                props <- Map.add key value props
                Task.FromResult true

    type App =
        { id: int64
          name: string
          status: int }

    type IAppGrain =
        inherit Orleans.IGrainWithGuidCompoundKey

        abstract member QueryUsers: unit -> Task<User list>
        abstract member AddUsers: User list -> Task<bool list>
        abstract member AddUser: User -> Task<bool>
        abstract member DelUser: User -> Task<bool>

    type AppGrain() =
        inherit Grain ()

        let mutable users: User seq = Seq.empty

        interface IAppGrain with
            member __.QueryUsers () =
                users
                |> Seq.toList
                |> Task.FromResult

            member __.AddUsers newUsers =
                newUsers
                |> List.map
                    (fun user ->
                        match Seq.tryFind (fun (_user: User) -> _user.id = user.id) users with
                        | Some _ ->
                            false
                        | None ->
                            users <- Seq.append [user] users
                            true
                    )
                |> Task.FromResult

            member __.AddUser user =
                match Seq.tryFind (fun (_user: User) -> _user.id = user.id) users with
                | Some _ ->
                    Task.FromResult false
                | None ->
                    users <- Seq.append [user] users
                    Task.FromResult true

            member __.DelUser user =
                users <- Seq.filter (fun _user -> _user.id <> user.id) users
                Task.FromResult true

    type IClientGrain =
        inherit Orleans.IGrainWithGuidKey

        abstract member QueryApps: unit -> Task<App list>
        abstract member AddApps: App list -> Task<bool list>
        abstract member AddApp: App -> Task<bool>
        abstract member DelApp: App -> Task<bool>

    type ClientGrain() =
        inherit Grain ()
        let mutable apps: Map<int64, App> = Map.empty

        interface IClientGrain with
            member __.QueryApps () =
                Map.toList apps
                |> List.map snd
                |> Task.FromResult

            member __.AddApps newApps =
                newApps
                |> List.map
                    (fun app ->
                        match Map.tryFind app.id apps with
                        | Some _ ->
                            false
                        | None ->
                            apps <- Map.add app.id app apps
                            true
                    )
                |> Task.FromResult

            member __.AddApp app =
                match Map.tryFind app.id apps with
                | Some _ ->
                    Task.FromResult false
                | None ->
                    apps <- Map.add app.id app apps
                    Task.FromResult true

            member __.DelApp app =
                apps <- Map.remove app.id apps
                Task.FromResult true

    let buildSiloHost () =
        let builder = SiloHostBuilder()
        let assembly = Reflection.Assembly.GetExecutingAssembly()
        builder
            .UseLocalhostClustering()
            .ConfigureApplicationParts(fun parts ->
                parts.AddApplicationPart(assembly)
                      .WithCodeGeneration() |> ignore)
            .Build()

    let start () =
        let t = task {
            let host = buildSiloHost ()
            do! host.StartAsync ()

            return host
        }

        t.Wait()
        t.Result

    let stop (host: ISiloHost) =
        let t = task {
            do! host.StopAsync()
        }
        t.Wait()

module Client =
    let buildClient () =
        let assembly = Reflection.Assembly.GetExecutingAssembly()
        let builder = ClientBuilder()
        builder
          .UseLocalhostClustering()
          .ConfigureApplicationParts(fun parts -> parts.AddApplicationPart(assembly).WithCodeGeneration() |> ignore )
          .Build()

    let start () =
        let t = task {
            let client = buildClient()
            do! client.Connect()

            return client
        }
        t.Wait()
        t.Result

    let stop (client: IClusterClient) =
        let t = task {
            do! client.Close()
        }

        t.Wait()