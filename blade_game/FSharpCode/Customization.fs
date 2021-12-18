module FSharpCode.Customization
open FSharpCode.Exts
open FSharpCode.Tezos
open FSharpPlus
open Godot
open Exts
open Netezos.Keys
open Netezos
open TezFs

type Equipment = {blade:int; character:int; color:int}

let default_equipment = {color=3000; blade = 1000 ; character=2000}
let mutable my_equipment = default_equipment

let default_licenses = set [1000;2000;3000]
let default_tmd = Map.ofList [
    3000, { TokenMetadata.name="Blue"; TokenMetadata.symbol= "Blue"; TokenMetadata.decimals = 0; shouldPreferSymbol = false; thumbnailUri = ""; textures = []; other = ["category", "color"]; }
//    3001, { TokenMetadata.name="Crimson"; TokenMetadata.symbol= "Crimson"; TokenMetadata.decimals = 0; shouldPreferSymbol = false; thumbnailUri = ""; textures = []; other = "color"; }
    2000, { TokenMetadata.name="Assassin #0"; TokenMetadata.symbol= "Assassin #0"; TokenMetadata.decimals = 0; shouldPreferSymbol = false; thumbnailUri = "res://images/player/bluecharacterbig.png"; textures = ["res://images/player/bluecharacterbig.png"]; other = ["category", "character"]; }
//    2001, { TokenMetadata.name="Assassin #1"; TokenMetadata.symbol= "Assassin #1"; TokenMetadata.decimals = 0; shouldPreferSymbol = false; thumbnailUri = "res://images/player/redcharacterbig.png"; textures = ["res://images/player/redcharacterbig.png"]; other = "character"; }
    1000, { TokenMetadata.name="Blade #0"; TokenMetadata.symbol= "Blade #0"; TokenMetadata.decimals = 0; shouldPreferSymbol = false; thumbnailUri = "res://images/blades/blade0.png"; textures = ["res://images/blades/blade0.png"]; other = ["category", "blade"]; }
//    1001, { TokenMetadata.name="Blade #1"; TokenMetadata.symbol= "Blade #1"; TokenMetadata.decimals = 0; shouldPreferSymbol = false; thumbnailUri = "res://images/blades/blade1.png"; textures = ["res://images/blades/blade1.png"]; other = "blade"; }
]
let mutable tmd_cached = default_tmd

let get_token_metadata = Tezos.get_token_metadata<(string * string) list> |>> Map.union default_tmd
let my_licenses () = Tezos.get_my_licenses() |>> Set.union default_licenses

let all_tokens = get_token_metadata |>> Map.keys |>> set |>> Set.toList |>> List.sort

let blades_filter = (fun x -> x >= 1000 && x < 2000)
let characters_filter = (fun x -> x >= 2000  && x < 3000)
let colors_filter = (fun x -> x >= 3000  && x < 4000)


let token_color =
    function
    | 3000 -> Colors.Blue
    | 3001 -> Colors.Crimson
    | 3002 -> Colors.Yellow
    | _ -> Colors.White
let token_texture tmd i =
    Map.tryFind i tmd
    >>= (fun (tm:TokenMetadata<(string * string) list>) -> List.tryHead tm.textures)
    |> Option.defaultValue "res://images/blades/blade0.png"
    |> ResourceLoader.load_texture
let display_blade (sblade:Node2D) (skin:Texture) (color:Godot.Color) =
    
        sblade.set_texture [
            "sprite", skin
            "sprite/outline", skin
            "sprite/trail", skin
        ]
        sblade.set_shader_param ["sprite/outline", "outline_color", color]
        let line = (sblade.n<Line2D> "line")
        line.DefaultColor <- color
let display_character_equipment (n:Node2D) (items:Equipment) =
    let blade = token_texture tmd_cached items.blade
    let character = token_texture tmd_cached items.character
    let color = token_color items.color
    
    n.set_texture [
        "char/vpc/body", character
        "char/corpse/top", character
        "char/corpse/bot", character
    ]
    display_blade (n.n"char/blades/rot/blade") blade color
    display_blade (n.n"char/blades/rot2/blade") blade color
    display_blade (n.n"char/blades/rot3/blade") blade color
    display_blade (n.n"char/blades/rot4/blade") blade color
    display_blade (n.n"char/blades/rot5/blade") blade color
    
    (n.n<Line2D>"line").DefaultColor <- color
    
    (n.n<Node>"after_image_spawner").Set("modulation", color)
    ()
    
    
let shop_scene = ResourceLoader.Load'<PackedScene> "res://scenes/shop.tscn"
let shop_item_scene = ResourceLoader.Load'<PackedScene> "res://scenes/shop_item.tscn"

let show_loading (n:Node2D) text b =
    n.Visible <- b
    n.set_text["hbox/txt", text] 

let async_loading (n:Node2D) text a =
    async{
        show_loading n text true
        let! x = a
        show_loading n text false
        return x
    }

let rec display_shop (n:Node2D) (n_loading:Node2D) category login_priv =
    async{
        try (n.n"shop").Free() with _ -> ()
        let shopn = spawn n shop_scene.Value
        (shopn.n<Button> <| sprintf "categories/%s"category).Pressed <- true
        shopn.set_text ["pub", Tezos.acc.PubKey.Address |> fanout (String.truncate 8) (String.rev >> String.truncate 4 >> String.rev) |> (fun (a,b) -> a + "..." + b) ]
        let! all_items = all_tokens
        let! license_shops = Tezos.get_license_shops
        let! tmd = get_token_metadata
        tmd_cached <- tmd
        do! download_imgs (tmd |> Map.values |> Seq.toList >>= (fun tm -> try tm.textures |> List.filter (fun p -> p.StartsWith "http") with _ -> []))
        let! inv = Tezos.my_inventory() |>> Map.ofSeq
        let! my_licenses = get_my_licenses() |>> Set.union default_licenses
        let! tez = Tezos.get_my_tez()
        let! blade = Tezos.get_my_rex()
        
        shopn.set_text [
            "my_money/tez/amount", string tez
            "my_money/blade/amount", string blade
        ]
        let reload_shop = display_shop n n_loading category login_priv
        let items_in_category =
            all_items  
            |> (match category with
                | "character" -> filter characters_filter
                | "blade" -> filter blades_filter
                | "color" -> filter colors_filter) 
        let grid = shopn.n<Control> "scroll/grid"
        
        if category <> "color" then
            let pn = spawn grid shop_item_scene.Value
            pn.set_visibility [
                "propose", true
                sprintf "propose/%s" category, true
            ]
            pn.child_on_events_add [
                ["propose/submit"], "pressed", fun _ ->
                    let name = pn.get_text "propose/name"
                    let tid = items_in_category |> maximum |> (+) 1
                    match pn.get_meta "propose" "file_path" with
                    Some filepath ->
                        async {
                            let! meta = skynet_upload_token_metadata name name 0 false filepath [filepath] ["category", category]
                            return! Tezos.propose_tokens [{initial_supply=[{address=Tezos.get_account_k (fun a -> a.PubKey.Address); nat=1}];  token_id=tid; token_info=meta |> System.Text.Encoding.UTF8.GetBytes |> K4os.Text.BaseX.Base16.ToHex }] 
                        } |>> printfn "Token proposal %A" >>= konst reload_shop |> async_loading n_loading "Processing Transaction" |> Async.StartImmediate
                    | _ -> ()
            ]
        for tid in items_in_category do
            let shop_item_n = spawn grid shop_item_scene.Value
            shop_item_n.set_visibility[ category, true ]
            shop_item_n.set_text ["name", tmd.[tid].name]
            match category with
            | "character" | "blade" -> shop_item_n.set_texture [category, token_texture tmd tid]
            | _ -> (shop_item_n.n<ColorRect>"color").Color <- token_color tid
            if inv |> Map.tryFind tid |> Option.defaultValue 0 = 1 then
                shop_item_n.set_visibility[ "set_price", true ]
                shop_item_n.set_text[ "set_price/price", license_shops |> Map.tryFind tid |>> snd |> Option.defaultValue 0 |> string]
                shop_item_n.set_visibility ["equip", true]
            else
                if my_licenses |> Set.contains tid then
                    shop_item_n.set_visibility ["equip", true]
                else
                    match license_shops |> Map.tryFind tid |>> snd with
                    | Some price ->
                        shop_item_n.set_visibility ["buy", true]
                        shop_item_n.set_text ["buy/button", string price]
                        
                    | _ -> ()
            shop_item_n.child_on_events_add[
                ["set_price/button"], "pressed", (fun _ -> Tezos.modify_license_shop tid (int <| shop_item_n.get_text "set_price/price") |>> printfn "Setting price for %A: %A" tid >>= (konst reload_shop) |> async_loading n_loading "Processing Transaction" |> Async.StartImmediate )
                ["buy/button"], "pressed", (fun _ -> Tezos.buy_license_tez (license_shops |> Map.tryFind tid |>> snd |> Option.defaultValue 0 |> int64  |> (*) ONE_TEZ |> int) tid |>> printfn "Buying %A with tez %A" tid >>= konst reload_shop |> async_loading n_loading "Processing Transaction" |> Async.StartImmediate )
                ["equip"], "pressed", (fun _ -> my_equipment <- match category with "character" -> { my_equipment with character = tid} | "blade" -> {my_equipment with blade = tid} | _ -> {my_equipment with color = tid} )
                ]
        shopn.child_on_events_add [
            ["categories/character"], "pressed", (fun _ -> display_shop n n_loading "character" login_priv |> Async.StartImmediate )
            ["categories/blade"], "pressed", (fun _ -> display_shop n n_loading "blade" login_priv |> Async.StartImmediate )
            ["categories/color"], "pressed", (fun _ -> display_shop n n_loading "color" login_priv |> Async.StartImmediate )
            ["categories/exit"], "pressed", (fun _ -> shopn.QueueFree() )
            ["import_priv"], "pressed", (fun _ -> let _ = login_priv (shopn.get_text "priv") in display_shop n n_loading category login_priv |> Async.StartImmediate)
        ]
        ()
    }