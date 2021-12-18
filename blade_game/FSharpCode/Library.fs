namespace FSharpCode


open System
open System.IO
open System.Net
open System.Net.Http
open System.Runtime.CompilerServices
open System.Text
//open Crypto.AES
open EnginePrime.EnginePrime.Networking
open Godot
open MBrace
open HOG.Tools.Physics
open Microsoft.FSharp.Reflection
open Newtonsoft.Json
open FSharp.Data
open FSharpx.Collections
open FSharpPlus
open Godot
open HOG.Tools
open SiaSkynet
type ObjectType = LH | RH | RL | LL | H | AXE

module Exts =
    let sproxy = "http://higher-order-games.net:9998/"
    let http_proxy = sprintf "http://higher-order-games.net:9998/%s"
    let (|Prefix|_|) (p:string) (s:string) = if s.StartsWith(p) then Some(s.Substring(p.Length)) else None
    let awaiter asy = (Async.StartAsTask asy).GetAwaiter()
    let inline curry f a b = f(a, b) 
    type Vector2 with
        static member from_vec (Vec(x, y)) = Vector2(float32 x, float32 y)
    let inline pos2d a = (body_position >> Vector2.from_vec) a
    let inline magic_position os i = os |> List.tryItem i  |> Option.map pos2d |> Option.defaultValue (Vector2(-1000.f, -1000.f)) 
    type Enum with static member parse<'T> value = System.Enum.Parse(typedefof<'T>, value, true) :?> 'T
    module Input =
        let (|JoystickList|_|) (s:string) = try Some(Enum.parse<JoystickList> s) with | e -> None
        let (|ButtonList|_|) (s:string) = try Some(Enum.parse<ButtonList> s) with | e -> None
        let (|KeyList|_|) (s:string) = try Some(Enum.parse<KeyList> s) with | e -> None
        type InputKey = JoystickButton of JoystickList | MouseButton of ButtonList | KeyboardButton of KeyList
            with
                static member string this = match this with | JoystickButton j -> "j " ++ string j | MouseButton m -> "m " ++ string m | KeyboardButton k -> "k " ++ string k
                static member TryParse s =
//                    printfn "parsing inp %s" s
                    match s with
                    | Prefix "j " (JoystickList j)-> Some(JoystickButton j)
                    | Prefix "m " (ButtonList b) -> Some(MouseButton b)
                    | Prefix "k " (KeyList k) -> Some(KeyboardButton k)
                    | _ -> None
        let action_pressed action i = Input.IsActionPressed (action + string i)
        let mouse_button_pressed (btn:Godot.ButtonList) = Input.IsMouseButtonPressed(int btn)
        let keyboard_button_pressed (btn:Godot.KeyList) = Input.IsKeyPressed(int btn)
//        let key_mouse_button_pressed (btn:Either<Godot.ButtonList, Godot.KeyList>) = match btn with | Left mbtn -> mouse_button_pressed mbtn | Right kbtn -> keyboard_button_pressed kbtn
        let joy_button_pressed (btn:JoystickList) i = Input.IsJoyButtonPressed(i, int btn)
        let button_pressed i (btn:InputKey) = match btn with | JoystickButton jb -> joy_button_pressed jb i | KeyboardButton kb -> keyboard_button_pressed kb | MouseButton mb -> mouse_button_pressed mb
        let keyboard_direction up down left right =
            let y = Input.GetActionStrength down - Input.GetActionStrength up
            let x = Input.GetActionStrength right - Input.GetActionStrength left
            (Vec.nondeterministically x y).normalized'
        let left_joystick_direction i =
            let h = Input.GetJoyAxis(i, 0)
            let h = if h > -0.2f && h < 0.2f then 0.0f else h
            let h = fract (int <| h * 1000.0f) 1000 
            let v = Input.GetJoyAxis(i, 1)
            let v = if v > -0.2f && v < 0.2f then 0.0f else v
            let v = fract (int <| v * 1000.0f) 1000 
            Vec(h, v) |> Vec.normalized //- Vec(0.2f, 0.2f).Normalized()
        let right_joystick_direction i =
            let h = Input.GetJoyAxis(i, 2)
            let h = if h > -0.2f && h < 0.2f then 0.0f else h
            let h = fract (int <| h * 1000.0f) 1000 
            let v = Input.GetJoyAxis(i, 3)
            let v = if v > -0.2f && v < 0.2f then 0.0f else v
            let v = fract (int <| v * 1000.0f) 1000 
            Vec(h, v) |> Vec.normalized //- Vec(0.2f, 0.2f).Normalized()
    module Queue =
        let pop q =
            let head = Queue.tryHead q
            let tail = try Queue.head q with | _ -> Queue.empty
            head, tail
    type AudioStreamPlayer with
        member this.Play'() =
            this.PitchScale <- float32 <| GD.RandRange(0.9, 1.1)
            this.Play()
        member this.play_if condition =
            if condition then do
                this.PitchScale <- float32 <| GD.RandRange(0.9, 1.1)
                this.Play()
    module List =
        let inline map_body ls = List.map body ls
    type List<'a> with

        static member cast<'a> (myList: obj list) =          
            match myList with
            | head::tail -> 
                match head with 
                | :? 'a as a -> a::(List.cast tail) 
                | _ -> List.cast tail
            | [] -> []
        static member zip' xs' ys' =
            let zip3 xs ys = 
                let rec loop r xs ys =
                    match xs,ys with 
                    | [],[]         -> r
                    | xh::xt,yh::yt -> loop ((xh,yh)::r) xt yt
                    | _ -> []
                loop [] xs ys
            zip3 xs' ys'
    
    let hash_string a = a |> hash |> abs |> string
    let fsp = MBrace.FsPickler.XmlSerializer()
    let aes_key = "5cd18d9a9ff108d4cc8ad3c410ddf9931862427646538211fac53527f92bf5cb"
//    let aes = new AES("5cd18d9a9ff108d4cc8ad3c410ddf9931862427646538211fac53527f92bf5cb")
    let encrypt, decrypt = (fun s -> EasyEncryption.AES.Encrypt(s, aes_key,"")), (fun s -> EasyEncryption.AES.Decrypt(s, aes_key,""))
    type File with
        static member exists = File().FileExists
        static member open' path flags success fail =
            try
                let f = new File()
                match f.Open(path, flags) with
                | Error.Ok ->
                    let ret = success f
                    f.Close()
                    ret
                | _e ->
                    eprintfn "bad open 1 %A" _e
                    fail
            with | _e ->
                     eprintfn "bad open 2 %A" _e
                     fail
        
        static member change path data_to_save =
            File.open' path File.ModeFlags.Write (fun f -> f.StoreLine(fsp.PickleToString data_to_save)) ()
        static member load<'a> path fail =
            File.open' path File.ModeFlags.ReadWrite (File.as_text >> fsp.UnPickleOfString<'a>) fail
        static member as_text (f:File) = try f.GetAsText() with | _ -> ""
        static member change_encrypted path data_to_save =
            printfn "before enc"
            let enc = encrypt (fsp.PickleToString data_to_save |> Encoding.UTF8.GetBytes |> Convert.ToBase64String)
            printfn "after enc"
            File.open' path File.ModeFlags.Write (fun f -> f.StoreLine(enc)) ()
        static member load_encrypted<'a> path fail =
            File.open' path File.ModeFlags.ReadWrite (File.as_text >> decrypt >> Convert.FromBase64String >> Encoding.UTF8.GetString >> fsp.UnPickleOfString<'a>) fail
    open Godot
    let hashpng = hash >> sprintf "%d.png"
    let (+/) p1 p2 = System.IO.Path.Combine(p1,p2)
    let dir = OS.GetUserDataDir()
    let cached_http_image_path url = dir +/ (url |> hashpng)  
    let download_imgs (urls: string list) =
        let wc = new WebClient()
        printfn "download beginsf %A" urls
        urls >>= (fun url -> let _ = printfn "try download %A" url in try if File.exists (cached_http_image_path url) then [] else [wc.AsyncDownloadFile(Uri (http_proxy url), cached_http_image_path url)] with _ -> []) |> Async.Sequential |>> ignore
    let rec private f path =
        match path with
        | Prefix "http" _ -> f (cached_http_image_path path) 
        | Prefix "res://" _ -> ResourceLoader.Load<Texture> path
        | _ -> let _ = printfn "load %A" path in let img = Image() in let _ = img.Load(path) in let txt = ImageTexture() in let _ = txt.CreateFromImage(img) in txt :> Texture
        | Prefix "user://" _ -> let _ = printfn "load %A" path in let img = Image() in let _ = img.Load(path) in let txt = ImageTexture() in let _ = txt.CreateFromImage(img) in txt :> Texture
        | _ -> failwithf "Could not load texture %A" path
    let private y = memoize f
                                                 
    type ResourceLoader with
        static member Load'<'a when 'a : not struct> p = lazy (ResourceLoader.Load<'a> p)
        static member load_texture = y
        static member load_resource_async path =
            let loader = ResourceLoader.LoadInteractive path
            let rec poll = async {
                let err = loader.Poll()
                if err = Error.FileEof
                then return loader.GetResource()
                else 
                    do! Async.Sleep 30
                    return! poll
            }
            poll
    type Sprite with
    
        member this.x_flip (dir : float32) =  
            this.FlipH <-
                if dir > 0.0f
                then false
                else
                   if dir < 0.0f
                    then true
                    else this.FlipH
        member this.y_flip (dir : float32) =  
            this.FlipV <-
                if dir > 0.0f
                then false
                else
                   if dir < 0.0f
                    then true
                    else this.FlipV
    type Node2D with
        
        
        member this.XFlipDir (dir : float32) =  
            this.Scale <-
                if dir > 0.0f
                then Vector2(abs (this.Scale.x), abs (this.Scale.y))
                else
                   if dir < 0.0f
                    then Vector2(-abs (this.Scale.x), abs (this.Scale.y))
                    else this.Scale
        member this.YFlipDir (dir : float32) =  
            this.Scale <-
                if dir > 0.0f
                then Vector2(abs (this.Scale.x), abs (this.Scale.y))
                else
                   if dir < 0.0f
                    then Vector2(abs (this.Scale.x), -abs (this.Scale.y))
                    else this.Scale
    type WebSocketClient'(url : string) as this=
        inherit WebSocketClient()
        let mutable incoming_messages = []
        let connect_cooldown_ms = 100
        let mutable last_connect_attempt = DateTime.Now
        do
            this.Connect("connection_established", this, "on_connected") |> ignore
            this.Connect("data_received", this, "on_message") |> ignore
            this.Connect("connection_closed", this, "on_connection_closed") |> ignore
            this.Connect("connection_error", this, "on_connection_failed") |> ignore

        member this.on_connected(protocol: obj[]) =

            (this.GetPeer 1).SetNoDelay true
            ()//this.connected <- true
        member this.on_message() =
            let msg = this.GetPeer(1).GetPacket()
            incoming_messages <- incoming_messages ++ [msg]
        member this.on_connection_closed(was_clean_close: obj[]) =
    //        GD.Print("Connection closed, current status: :", this.GetConnectionStatus)
            this.ConnectToUrl url
            ()//this.connected <- false
        member this.on_connection_failed() = ()//this.connected <- false
        interface Client with
            member this.poll() = this.Poll()
            member this.connect url =
                if this.GetConnectionStatus() = NetworkedMultiplayerPeer.ConnectionStatus.Disconnected && (DateTime.Now.Subtract last_connect_attempt).Milliseconds > connect_cooldown_ms
                then
                    last_connect_attempt <- DateTime.Now
                    this.ConnectToUrl url |> ignore // fun x -> GD.Print("Connection: ", x)
            member this.is_connected() = match this.GetConnectionStatus() with NetworkedMultiplayerPeer.ConnectionStatus.Connected  -> true | _ -> false
            member this.consume_msg() =
                let ret = incoming_messages
                incoming_messages <- empty
                ret
            member this.send msg = if (this :> Client).is_connected() then do for m in msg do this.GetPeer(1).PutPacket(m) |> ignore
    let union_from_string<'a> (s:string) =
        match FSharpType.GetUnionCases typeof<'a> |> Array.filter (fun case -> case.Name = s) with
        |[|case|] -> Some(FSharpValue.MakeUnion(case,[||]) :?> 'a)
        |_ -> None

    let spawn (parent:Node) (n:PackedScene) = let n = n.Instance() in let _ = parent.AddChild n in n
    type Doer(lambda) =
        inherit Node2D()
        member this.lambda() : unit = lambda()
    type TaskAwaiter<'a> with
        member this.unwrap_else ready not_ready = if this.IsCompleted then ready <| this.GetResult() else not_ready
    type Control with
        member this.set_center_position p = this.SetPosition(p - this.RectSize/2.f)
        member this.get_center_position = this.RectPosition + this.RectSize/2.f
    type Color with
        member this.with_alpha a = new Color(this.r, this.g, this.b, a)
        static member lerp dt (c1:Color) (c2:Color) = Color(Mathf.Lerp(c1.r, c2.r, dt), Mathf.Lerp (c1.g, c2.g, dt),Mathf.Lerp (c1.b, c2.b, dt), Mathf.Lerp(c1.a, c2.a, dt))
        member this.with_intensity i = new Color(this.r * i, this.g*i, this.b*i, this.a)
    type Node with
        member this.get_node<'a when 'a:> Node> s = lazy(this.GetNode(new NodePath(s)) :?> 'a)
//        member this.n = memoize <| fun s -> this.GetNode(new NodePath(s)) :?> 'a
        member this.n<'a when 'a:> Node> s = this.GetNode(new NodePath(s)) :?> 'a
        member this.get_node_children<'a> s = lazy( [for c in (this.get_node s).Value.GetChildren() -> c :?> 'a] )
        member this.get_node_children'<'a> s = [for c in (this.n s).GetChildren() -> c :?> 'a]
        member this.get_children<'a>() = [for c in this.GetChildren() -> c :?> 'a]
        member this.get_all_of_type<'a>() = this.get_children<Node>()|> Seq.collect  (fun n -> match n with | :? 'a as n -> seq [n] | n -> n.get_all_of_type<'a>())
        member this.on_event_add e lambda =
            let d = new Doer(lambda)
            this.AddChild(d)
            this.Connect(e, d, "lambda", flags=uint32 Object.ConnectFlags.Deferred) |> ignore
        member this.play_sfx n = (this.n<AudioStreamPlayer> n).Play()
        member this.play_anim n a = (this.n<AnimationPlayer> n).Play a
        member this.stop_anim n = (this.n<AnimationPlayer> n).Stop()
        member this.play_sfx_once n = let n = (this.n<AudioStreamPlayer> n) in tap (fun _ -> if n.Playing then () else n.Play()) n
        member this.play_sfx_if b n = let n = (this.n<AudioStreamPlayer> n) in if n.Playing then () else if b then n.Play() else n.Stop()
        member this.stop_sfx n = let n = (this.n<AudioStreamPlayer> n) in n.Stop()
        member this.set_shader_param npvs = for n, p, v in npvs do ( match this.n<Node> n with
                                                                       | :? Node2D as n -> match n.Material with | :? ShaderMaterial as m -> m.SetShaderParam(p, v) | _ -> failwithf "Could not set shader material property %A for %A with value %A " p n v
                                                                       | :? Control as n -> match n.Material with | :? ShaderMaterial as m -> m.SetShaderParam(p, v) | _ -> failwithf "Could not set shader material property %A for %A with value %A " p n v
                                                                       | _ -> failwithf "Could not set shader material property %A for %A with value %A " p n v ) 
        member this.get_shader_param<'a> n p = ( match this.n<Node> n with
                                                               | :? Node2D as n -> match n.Material with | :? ShaderMaterial as m -> m.GetShaderParam(p) :?> 'a | _ -> failwithf "Could not get shader material property %A for %A" p n 
                                                               | :? Control as n -> match n.Material with | :? ShaderMaterial as m -> m.GetShaderParam(p) :?> 'a | _ -> failwithf "Could not get shader material property %A for %A" p n
                                                               | _ -> failwithf "Could not get shader material property %A for %A " p n )
        member this.modify_shader_param n p k = this.set_shader_param [n,p,this.get_shader_param n p |> k] 
        member this.get_meta<'a> n m = let n = this.n n in if n.HasMeta(m) then Some(n.GetMeta(m) :?> 'a) else None
        member this.set_meta<'a> n m (fv:'a option -> 'a) = let new_value = this.get_meta<'a> n m |> fv in (this.n n).SetMeta(m, new_value)
        member this.set_alpha nas = for n, a in nas do match (this.get_node<Node>n).Value with | :? Node2D as n -> n.Modulate <- n.Modulate.with_alpha a| :? Control as n -> n.Modulate <- n.Modulate.with_alpha a
        member this.set_text lts = for (l, t) in lts do match (this.get_node<Node> l).Value with | :? LineEdit as l -> l.Text <- t | :? Label as l -> l.Text <- t | :? RichTextLabel as l -> l.Text <- t | :? LinkButton as l -> l.Text <- t| :? Button as l -> l.Text <- t| _ -> failwithf "Failed setting text %A %A" l t 
        member this.get_text n = (this.n n).Get "text" :?> string
        member this.get_spinbox_value n = (this.n<SpinBox> n).Value
        member this.get_option_button_selected n = (this.n<OptionButton> n).Selected
        member this.set_texture lts =
            for (l, t) in lts do
                match this.n<Node> l with
                | :? Sprite as l -> l.Texture <- t
                | :? TextureRect as l -> l.Texture <- t
                | :? TextureButton as b -> b.TextureNormal <- t
                | :? NinePatchRect as b -> b.Texture <- t
                | n -> failwithf "Failed setting texture %A %A %A" l t n 
        member this.set_texture_lazy_if lts =
            for (l, f) in lts do
                match (this.n<Node> l), f l with
                | :? Sprite as n, Some (t:Lazy<Texture>) -> n.Texture <- t.Value | _ -> ()
                | :? TextureRect as n, Some (t:Lazy<Texture>) -> n.Texture <- t.Value | _ -> ()
                | :? TextureButton as n, Some (t:Lazy<Texture>) -> n.TextureNormal <- t.Value | _ -> ()
                | :? NinePatchRect as n, Some (t:Lazy<Texture>) -> n.Texture <- t.Value | _ -> ()
                | _ -> failwithf "Failed setting texture %A" l
        member this.set_visibility nbs = for (n, b) in nbs do match (this.n<Node> n) with | :? Node2D as n -> n.Visible <- b | :? Control as n -> n.Visible <- b | _ -> failwithf "Failed setting visibility %A %A" n b
        member this.set_visibility' nbs = for (n:Node, b) in nbs do match n with | :? Node2D as n -> n.Visible <- b | :? Control as n -> n.Visible <- b | _ -> failwithf "Failed setting visibility %A %A" n b
        member this.set_emitting nbs = for (n, b) in nbs do match (this.n<Node> n) with | :? CPUParticles2D as n -> (if n.Emitting = b then () else n.Emitting <- b) | :? Particles2D as n -> (if n.Emitting = b then () else n.Emitting <- b) | _ -> failwithf "Failed setting emitting %A %A" n b
        member this.toggle_visibilities nbs = for (n, b) in nbs do match (this.n<Node> n) with | :? Node2D as n when b -> n.Visible <- not n.Visible | :? Control as n when b -> n.Visible <- not n.Visible | _ when not b -> () | _ -> failwithf "Failed setting visibility %A %A" n b
        member this.toggle_visibility ns = for n in ns do match (this.n<Node> n) with | :? Node2D as n -> n.Visible <- not n.Visible | :? Control as n -> n.Visible <- not n.Visible | _ -> failwithf "Failed setting visibility %A" n
        member this.set_texture_and_visibility ntvs = for (n, t, v) in ntvs do let _ = this.set_texture [n, t] in this.set_visibility [n, v]
        member this.child_on_event_add cs e l = for c in cs do (this.n<Node> c).on_event_add e l
        member this.child_on_events_add csel = for cs, e, l in csel do for c in cs do (this.n<Node> c).on_event_add e l
        member this.add_event_to_each_child sefs =
            for s, e, f in sefs do
                for i, n in List.indexed (this.get_node_children'<Node> s) do
                    n.on_event_add e (f i)
    
    let init_and_process<'uid, 'ent, 'node when 'uid : comparison and 'node :> Node> (root:Node) (scene:PackedScene) (entities:Map<'uid,'ent>) (child:string) i f =
        let children_root = root.GetNode (NodePath child)
        let node_children =  root.get_node_children'<'node> child
        let ent_uids = entities |> Map.keySet
        let uid_nodes = node_children |> List.map (fun n -> n.GetMeta "uid" :?> 'uid, n) |> Map.ofList
        let node_uids = uid_nodes |> Map.keySet
        let new_uids = Set.difference ent_uids node_uids
        let new_uid_nodes =
            new_uids
            |> Seq.map (fun uid ->
                let instance = scene.Instance() :?> 'node
                instance.SetMeta("uid", uid)
                children_root.AddChild instance
                i entities.[uid] instance
                uid, instance
            ) |> Map.ofSeq
        let node_uids_to_destroy = Set.difference node_uids ent_uids
        for uid in node_uids_to_destroy do
            uid_nodes.[uid].QueueFree()
        let uid_nodes = Map.union uid_nodes new_uid_nodes
        for uid in ent_uids do
            f entities.[uid] uid_nodes.[uid]
               
    let manifest<'uid, 'ent, 'node when 'uid : comparison and 'node :> Node> (root:Node) (scene:PackedScene) (entities:Map<'uid,'ent>) (child:string) f =
        init_and_process  root scene entities child (konst ignore) f
    module Vector2 =
        let inline lerp (dt:float32) (from:Vector2) towards  = from.LinearInterpolate(towards, dt)
    module Mathf =
        let inline lerp from towards dt = Mathf.Lerp(from, towards, dt)
    let inline visualize (dt:float32) (sprites : (Node2D list)) entities cont =
        for i, sp in List.indexed sprites do
            sp.Position <- entities |> List.map body |> List.tryItem i |> function | Some p -> let target = Vector2.from_vec p.position in (if sp.Position.DistanceTo target> 200.f then target else sp.Position.LinearInterpolate(target, dt)) | _ -> Vector2(-1000.f, -1000.f)
            sp.Visible <- entities |> List.map body |> List.tryItem i |> function | Some _ -> true | _ -> false
            sp.RotationDegrees <- entities |> List.map body |> List.tryItem i |> function | Some _ -> sp.RotationDegrees | _ -> 0.f
            entities |> List.tryItem i |> function | Some e -> cont sp e| _ -> ()
    let inline visualize' (dt:float32) (sprites : (Node2D list list)) entities cont =
        for i, sps in List.indexed (List.transpose sprites) do
            for sp in sps do
                sp.Position <- entities |> List.map body |> List.tryItem i |> function | Some p -> let target = Vector2.from_vec p.position in (if sp.Position.DistanceTo target> 200.f then target else sp.Position.LinearInterpolate(target, dt)) | _ -> Vector2(-1000.f, -1000.f)
                sp.Visible <- entities |> List.map body |> List.tryItem i |> function | Some _ -> true | _ -> false
                sp.RotationDegrees <- entities |> List.map body |> List.tryItem i |> function | Some _ -> sp.RotationDegrees | _ -> 0.f
            entities |> List.tryItem i |> function | Some e -> cont sps e| _ -> ()
    type CanvasItem with
        member this.draw_shape =
            let color = Colors.Aqua.with_alpha 0.3f
            function
            | {area = Circle r ; position = p} -> this.DrawCircle(Vector2.from_vec p, float32 r, color)
            | {area = Rect(w, h) ; position = p} -> this.DrawRect(Rect2(Vector2.from_vec (p - Vec(w, h)), 2.f * float32 w, 2.f * float32 h), color)
        
    type Tween with
        member this.interpolate node property start final duration trans ease=
            this.InterpolateProperty(node, new NodePath(property), start, final, duration, trans, ease) |> ignore
            this.Start() |> ignore
            this
            
    module JsonConvert =
        let deserialize_object<'a> fail s = try JsonConvert.DeserializeObject<'a> s with | _ -> fail
        
            

    let kencrypt, kdecrypt = (fun key s -> EasyEncryption.AES.Encrypt(s, key,"")), (fun key s -> EasyEncryption.AES.Decrypt(s, key,""))
    let skynet_portal = http_proxy "https://siasky.net"
    let skynet_upload_string (s:string) = 
        SiaSkynetClient(skynet_portal).UploadFileAsync("token_metadata", MemoryStream(Encoding.UTF8.GetBytes(s))) |>> (fun res -> sprintf "https://siasky.net/%s" res.Skylink) |> Async.AwaitTask
    let skynet_upload_img path =
        SiaSkynetClient(skynet_portal).UploadFileAsync("token_texture", File.OpenRead path) |>> (fun r -> sprintf "https://siasky.net/%s" r.Skylink) |> Async.AwaitTask
    type TokenMetadata<'a> = {name:string; symbol:string; decimals:int; shouldPreferSymbol:bool; thumbnailUri:string; textures: string list; other:'a}
    let skynet_upload_token_metadata name symbol decimals prefer_symbol thumbnail (textures:string list) (other:'a) =
        async{
            let! thumbnail = skynet_upload_img thumbnail
            let! textures = textures |>> skynet_upload_img |> Async.Parallel |>> List.ofArray
            let tm = {
                name = name
                symbol = symbol
                decimals = decimals
                shouldPreferSymbol = prefer_symbol
                thumbnailUri = thumbnail
                textures = textures
                other = other
            }
            return! skynet_upload_string (Newtonsoft.Json.JsonConvert.SerializeObject tm)
        }
    type SkyDBAccount = {pub:byte[]; priv:byte[]} with
        member this.spub = this.pub |> K4os.Text.BaseX.Base64.ToBase64
        member this.spriv = this.priv |> K4os.Text.BaseX.Base64.ToBase64
        override this.ToString() = sprintf "{ pub = %s; priv = %s }" this.spub (String.truncate 4 this.spriv + "..." + this.spriv |> String.rev |> String.truncate 4 |> String.rev)
    let skydb_login pass = let struct (pub, priv) = SiaSkynetClient.GenerateKeys pass in {pub=pub;priv=priv}
    let skydb_set<'a> acc key (value:'a) = let value = JsonConvert.SerializeObject value in SiaSkynetClient(skynet_portal).SkyDbSetAsString(acc.priv, acc.pub, key, value) |> Async.AwaitTask    
    let skydb_set_private<'a> (acc:SkyDBAccount) key (value:'a) = let value = JsonConvert.SerializeObject value |> kencrypt acc.spriv in SiaSkynetClient(skynet_portal).SkyDbSetAsString(acc.priv, acc.pub, key, value) |> Async.AwaitTask    
    let skydb_get<'a> pub key = SiaSkynetClient(skynet_portal).SkyDbGetAsString(pub, key) |>> JsonConvert.DeserializeObject<'a> |> Async.AwaitTask
    let skydb_get_private<'a> acc key = SiaSkynetClient(skynet_portal).SkyDbGetAsString(acc.pub, key) |>> kdecrypt acc.spriv |>> JsonConvert.DeserializeObject<'a> |> Async.AwaitTask
    
module Seq =
    let fromGDArray (xs:Collections.Array) = 
        seq {
            for x in xs do
                yield x
            } 