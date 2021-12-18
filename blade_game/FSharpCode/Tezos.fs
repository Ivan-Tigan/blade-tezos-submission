module FSharpCode.Tezos

open System
open System.Collections.Generic
open System.IO
open System.Net
open System.Net.Http
open System.Text
open FSharpCode.Exts
open FSharpCode.Exts
//open Godot.Collections
open Godot
open HOG.HTTP
//open HOG.Database.Client
open FSharpPlus
open Netezos.Encoding
open Netezos.Forging.Models
open Netezos.Rpc

//let granada_rpc_url = http_proxy "https://granadanet.api.tez.ie/"
let rpc_url = http_proxy "https://rpc.tzkt.io/hangzhou2net/"
let api_read_url = http_proxy "https://api.hangzhou2net.tzkt.io/v1/"
let tez_rpc = TezosRpc(rpc_url)
let read_api x = Printf.kprintf (fun s -> api_read_url + s) x

let fa2_address = "KT1MZpGJtgziGiVUp1o1jJSj91iB3K44dFnk"
let license_address = "KT1Ws2ubHubUToZk9Mwr4gAy9iR5KdiJoT9h"
let ah_address = "KT1WcfBQTXZfApnksN34ZjR2mEprVKcKLZVy"

//let offers_id = "117051"
//let token_metadata_id = "164899"
//let ledger_id = "164896"
//let token_proposals_id = "164900"
//let license_shops_id = "164902"
//let licenses_ledger_id = "164901"

type TokenProposal<'a> = {initial_supply: (string * int) list; token_id: int; token_info:TokenMetadata<'a>} 
type AuctionOffer = {end_: DateTime; price: (string * int * int); product: (int * int * int) list list; seller: string;start_: DateTime}
type fa2 = {nat:int;address:string}
type price = {``or``:fa2;nat:int;address:string}
type ahoffer =  {end_: DateTime; price: price; product: Dictionary<string, int> list list; seller: string;start_: DateTime}
type kv<'k,'v> = {key:'k; value:'v}
type token_info = {token_info:Dictionary<string, string>}
type tokenprop = {initial_supply: (string * int) list; token_id: int; token_info:string}
let get_meta_from_url x = x |> K4os.Text.BaseX.Base16.FromHex |> Encoding.UTF8.GetString |> http_proxy |> http.get<TokenMetadata<'a>>
let get_token_metadata<'a> = http.get<kv<int, token_info> list> (read_api "contracts/%s/bigmaps/token_metadata/keys?select=key,value&active=true" fa2_address) |>> List.map (fun kv -> kv.value.token_info.[""]  |> get_meta_from_url<'a> |>> fun tm -> kv.key, tm) |>> Async.Parallel |> Async.join |>> Map.ofSeq
let run = Async.RunSynchronously

let get_token_proposals<'a> =
    http.get<kv<string, tokenprop list> list> (read_api "contracts/%s/bigmaps/token_proposals/keys?select=key,value&active=true" fa2_address)
    |>> List.map (fun kv -> kv.value |>> (fun v -> v.token_info |> get_meta_from_url |>> (fun x -> {TokenProposal.token_id = v.token_id; initial_supply = v.initial_supply; token_info= x})) |> Async.Parallel |> Async.map (fun v -> kv.key, List.ofArray v)) |>> Async.Parallel |> Async.join |>> Map.ofArray
                                                                                                                                                                                                             
let get_balance (pub_hash:string) = http.get<float> (read_api "accounts/%s/balance" pub_hash) |>> fun x -> x/1000000.0

let get_auction_house_offers =
    http.get<kv<string, ahoffer> list> (read_api "contracts/%s/bigmaps/offers/keys?select=key,value&active=true" ah_address) |>>
        (List.map (fun res ->
            (res.key, {
                AuctionOffer.end_ = (res.value.end_).ToUniversalTime()
                price = (res.value.price.address, int res.value.price.``or``.nat, int res.value.price.nat)
                product = res.value.product |> List.map (List.map (fun dict -> (dict.["nat_0"], dict.["nat_1"], dict.["nat_2"]))) 
                seller = res.value.seller
                start_ = (res.value.start_).ToUniversalTime()
                }
        ))>> Map.ofList)
let get_my_auction_house_offers (pub_hash: string) (map_id:string) =
    http.get<kv<string,ahoffer> list> (read_api "contracts/%s/bigmaps/offers/keys?value.seller=%s&select=key,value&active=true" ah_address pub_hash) |>>
        (List.map (fun res ->
            (res.key, {
                AuctionOffer.end_ = (res.value.end_).ToUniversalTime()
                price = (res.value.price.address, int res.value.price.``or``.nat, int res.value.price.nat)
                product = res.value.product |> List.map (List.map (fun dict -> (dict.["nat_0"], dict.["nat_1"], dict.["nat_2"]))) 
                seller = res.value.seller
                start_ = (res.value.start_).ToUniversalTime()
                }
        ))>> Map.ofList)

let get_ledger (map_id: string) =
    http.get<kv<Dictionary<string, string>, int> list> (read_api "contracts/%s/bigmaps/ledger/keys?select=key,value&active=true" map_id) |>> (List.choose (fun res -> let q = int res.value in if q = 0 then None else Some ((res.key.["address"], int res.key.["nat"]), res.value)) >> Map.ofList)

let get_my_ledger (pub_hash: string) (map_id: string) =
    http.get<kv<Dictionary<string, string>, int> list> (read_api "contracts/%s/bigmaps/ledger/keys?key.address=%s&select=key,value&active=true" map_id pub_hash) |>> (List.choose (fun res -> let q = res.value in if q = 0 then None else Some ((res.key.["address"], int res.key.["nat"]), res.value)) >> Map.ofList)

let get_fa2_quantity (map_id:string) (pub_hash:string) (token_id:int) =
    http.get<int list> (read_api "contracts/%s/bigmaps/ledger/keys?key.address=%s&key.nat=%d&select=value&active=true" map_id pub_hash token_id) |>> tryHead |>> Option.defaultValue 0
        
let get_fa2_quantities (map_id:string) (l:(string * int) list) =
    l |>> (fun (a,b) -> get_fa2_quantity map_id a b) |> List.toSeq |> Async.Sequential |>> List.ofArray
    

let mutable acc = TezFs.random_account()//acc_base58 "edskRqp5FXa4YmZFh6tvKLpYR6WmwqvoSEreQCafxeeJfwdshUknrv3bxSaiQPcib9fiGAAfKuZPzyETZr5Uy7nxVUMjUbinnM"
let login priv =
    let _acc = TezFs.acc_base58 priv in
    acc <- _acc
    acc

let get_account_k k = k acc
let get_account() = acc
let is_revealed (k:Netezos.Keys.Key) = http.get<unit list> (read_api "operations/reveals?sender=%s&status=applied" k.PubKey.Address) |>> List.isEmpty |>> not
let send_single txn = async{ try return! TezFs.send_batch_txns is_revealed tez_rpc acc [| txn |]  |>> Some with e -> let _ = eprintfn "Error transaction: %A" e in return None} 
let tez_call tez contract_address entrypoint arg = TezFs.call_entrypoint_txn tez_rpc acc contract_address entrypoint arg |>> tap (fun tx -> let _ = tx.StorageLimit <- 1000 in tx.Amount <- int64 tez ) >>= send_single
type nat01 = {nat_0:int; nat_1:int}
type addressnat0 = {address:string;nat:int}
type token_type_param_fa2 = {fa2:addressnat0}
type token_type_param_tez = {tez:bool}
type addressornat1 = {address:string;``or``:token_type_param_fa2;nat:int}
type nat012 = {nat_0:int;nat_1:int;nat_2:int}
type offer = {seller:string; price:addressornat1; start_:DateTime;end_:DateTime;product:nat012 list list}
let bid (offer_id:int) (quantity:int) = tez_call 0 ah_address "bid" [{nat_0=offer_id; nat_1= quantity}]
let post_ah_offer start_ end_ (tid, price) product = tez_call 0 ah_address "post" [{seller = acc.PubKey.Address; price = {addressornat1.address=acc.PubKey.Address;``or``={fa2={address=fa2_address;nat=0}};addressornat1.nat=price}; start_ = start_; end_=end_; product = product}]
type TokenProp = { initial_supply: addressnat0 list; token_id:int; token_info:string }
let propose_tokens (tokens: TokenProp list) = tez_call 0 fa2_address "propose_tokens" tokens
let inventory pubh = get_my_ledger pubh fa2_address |>> tap (printfn "GO MY LEDGER %A %A" pubh) |>> Map.choosei(fun (k,t) v -> if k = pubh then Some (t,v) else None) |>> Map.values

type some_param<'a> = {some:'a}
type price_param={``Right True``:int}
type shop_param={price:kv<token_type_param_tez,int> list; revenue_owner:string}
type modify_shop_param = {shop:shop_param; tid:int}
let modify_license_shop tid price = tez_call 0 license_address "modify_shop" {shop={price=[{key={tez=true}; value=price}]; revenue_owner=get_account_k (fun a -> a.PubKey.Address)}; tid=tid}
type buy_license_param = {recipient:string; tid:int; token_type:token_type_param_tez}
let get_license_shops = 
    http.get<kv<int, shop_param> list> (read_api "contracts/%s/bigmaps/license_shop/keys?select=key,value&active=true" license_address)
    |>> (fun x -> x |>> (fun kv -> kv.key, (kv.value.revenue_owner, (List.head kv.value.price).value)) |> Map.ofList)
let buy_license_tez tez tid = tez_call tez license_address "buy" {recipient = get_account_k(fun a -> a.PubKey.Address); tid=tid; token_type = {tez=true}} 

let get_my_licenses () = get_my_ledger acc.PubKey.Address license_address |>> Map.keys |>> Seq.map snd |>> set
let my_inventory() = inventory acc.PubKey.Address
let get_my_rex() = get_fa2_quantity fa2_address acc.PubKey.Address 0
let get_my_tez() = get_balance acc.PubKey.Address

let batch_send_tez k trs = List.map (fun (a, amt) -> TezFs.send_tez_txn k a amt :> ManagerOperationContent) trs |> Array.ofList |> TezFs.send_batch_txns is_revealed tez_rpc k 