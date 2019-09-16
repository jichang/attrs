namespace Feblr.Attrs.Engine

open System
open Orleans
open Orleans.Runtime.Configuration
open Orleans.Hosting
open System.Threading.Tasks
open FSharp.Control.Tasks.V2

module Host =
    type IAppGrain =
        inherit Orleans.IGrainWithGuidKey

    type AppGrain() =
        inherit Grain ()

    type IUserGrain = 
        inherit Orleans.IGrainWithIntegerKey
        abstract member UpdateFlag : string -> bool -> Task<bool>
        abstract member UpdateProp : string -> string -> Task<bool>

    type UserGrain() =
        inherit Grain ()

        let mutable flags = Map.empty
        let mutable props = Map.empty

        interface IUserGrain with 
            member __.UpdateFlag (key : string) (value: bool) : Task<bool> =
                flags <- Map.add key value flags
                Task.FromResult true

            member __.UpdateProp (key : string) (value: string) : Task<bool> = 
                props <- Map.add key value props
                Task.FromResult true

    let buildSiloHost () =
        let builder = SiloHostBuilder()
        builder
            .UseLocalhostClustering()
            .ConfigureApplicationParts(fun parts ->
                parts.AddApplicationPart(typeof<UserGrain>.Assembly)
                      .AddApplicationPart(typeof<IUserGrain>.Assembly)
                      .WithCodeGeneration() |> ignore)
            .Build()

    let start name =
        let t = task {
            let host = buildSiloHost ()
            do! host.StartAsync ()

            printfn "Press any keys to terminate..."
            Console.Read() |> ignore

            do! host.StopAsync()

            printfn "SiloHost is stopped"
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
