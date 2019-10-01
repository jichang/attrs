namespace Feblr.Attrs
open System

open Feblr.Attrs.Engine.Host

module OAuth =
    open Hopac
    open HttpFs.Client
    open Thoth.Json.Net

    let extraCoders =
        Extra.empty
        |> Extra.withInt64
        |> Extra.withDecimal

    type OAuthConfig =
        { endpoint: string
          serverId: string
          clientId: string
          clientSecret: string
          redirectUri: string
          scopeName: string
          state: string
          ticketEndpoint: string }

    type OAuthTicket =
        { open_id: string
          access_token: string
          refresh_token: string }

    let oauth (code: string) (oauthCfg: OAuthConfig) = job {
        let body =
            {| code = code
               client_id = oauthCfg.clientId
               client_secret = oauthCfg.clientSecret |}
        let json = Encode.Auto.toString(4, body)
        let request =
            Request.createUrl Post oauthCfg.ticketEndpoint
            |> Request.bodyString json
        use! response = getResponse request
        let! bodyStr = Response.readBodyAsString response
        return Decode.Auto.fromString<OAuthTicket>(bodyStr)
    }

    let queryApps (url: string) (ticket: OAuthTicket) = job {
        let request =
            Request.createUrl Get url
            |> Request.queryStringItem "open_id" ticket.open_id
            |> Request.queryStringItem "access_token" ticket.access_token
        use! response = getResponse request
        let! bodyStr = Response.readBodyAsString response
        return Decode.Auto.fromString<App list>(bodyStr, false, extraCoders)
    }
