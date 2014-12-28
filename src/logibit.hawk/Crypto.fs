﻿module logibit.hawk.Crypto

open System
open System.Security.Cryptography
open System.Text

open logibit.hawk
open logibit.hawk.Types

[<Literal>]
let header_version = "1"

let gen_norm_str (``type`` : string) (opts : FullAuth) =
  String.Concat
    [ yield "hawk."
      yield header_version
      yield "."
      yield ``type``
      yield "\n"
      yield sprintf "%O\n" opts.timestamp
      yield sprintf "%s\n" opts.nonce
      yield sprintf "%O\n" opts.``method``
      yield sprintf "%s\n" opts.resource
      yield sprintf "%s\n" (String.to_lower_inv opts.host)
      yield sprintf "%d\n" opts.port
      yield sprintf "%s\n" (opts.hash |> Option.or_default "")
      match opts.ext with
      | None -> ()
      | Some ext ->
        let ext = ext.Replace("\\", "\\\\").Replace("\n", "\\n")
        yield ext
      yield "\n"
      match opts.app with
      | None -> ()
      | Some app ->
        yield sprintf "%s\n" app
        match opts.dlg with
        | None -> ()
        | Some dlg -> yield sprintf "%s\n" dlg
      ]

let init_payload_hash (algo : Algo) content_type =
  let h = Hash.mk algo.DotNetString (sprintf "hawk.%s.payload\n" header_version)
  Hash.update h (sprintf "%s\n" (Hoek.parse_content_type content_type))
  h

let calc_payload_hash (payload : _ option) (algo : Algo) (content_type : _ option) =
  let hash = init_payload_hash algo content_type
  payload |> Option.or_default "" |> Hash.update hash
  "\n" |> Hash.update_final hash |> Convert.ToBase64String

/// Create a base64 encoded hmac signature of a UTF-8 encoding of the concatenated strings,
/// i.e. base64(hmac(K, body))
let create_hmac (algo : Algo) (key : string) (body : string) =
  let hmac = HMAC.Create algo.DotNetHmacString
  hmac.Key <- Encoding.UTF8.GetBytes key
  let buf = body |> Encoding.UTF8.GetBytes
  hmac.ComputeHash buf |> Convert.ToBase64String

let calc_mac (``type`` : string) (opts : FullAuth) =
  let normalised = gen_norm_str ``type`` opts
  create_hmac opts.credentials.algorithm opts.credentials.key normalised