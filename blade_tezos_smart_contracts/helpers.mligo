

type migration_status = Waiting of address | Working | Emigrated of address

let next_nat (n:nat) = (22695477n*n+1n) mod (Bitwise.shift_left 2n 32n)

let identity (x:_a) : _a = x
let konst (a:_a) (_b:_b) : _a = a 
let item1of2 ((a,_b):_a * _b) : _a = a
let item2of2 ((_a,b):_a * _b) : _b = b

module ListHelpers = struct
    let try_item (i:nat) (xs:_a list) : _a option = let (_, res) = List.fold_left (fun (((acci, acc), x): (nat * _a option) * _a) -> acci + 1n, (if acci = i then Some x else acc)) (0n, (None: _a option)) xs in res
    let item_unsafe (i:nat) (xs:_a list) : _a = match (try_item i xs : _a option) with Some x -> x | _ -> failwith "Index out of bounds"
    let concat (xs: _a list) (ys: _a list) : _a list = List.fold_right (fun ((x, acc): _a * _a list) -> x::acc) xs ys 
    let bind (fm: _a -> _b list) (xs: _a list) : _b list = List.fold_left (fun ((acc, x): _b list * _a) -> concat acc (fm x) ) ([]: _b list) xs
    let bindi (fm: nat -> _a -> _b list) (xs: _a list) : _b list = let (_, r) = List.fold_left (fun (((i, acc), x): (nat * _b list) * _a) -> i+1n, concat acc (fm i x) ) (0n, ([]: _b list)) xs in r
    let mapi (f: nat -> _a -> _b) (xs: _a list) : _b list = let (_, res) = List.fold_right (fun ((x, (acci, acc)) : _a * (nat * _b list)) -> abs(acci-1n), (f acci x)::acc) xs (abs(List.length xs - 1n), ([]:_b list)) in res
    let fold_right_i (f: nat -> (_a * _b) -> _b) (xs: _a list) (acc:_b) : _b = let (_, res) = List.fold_right (fun ((x, (acci, acc)) : _a * (nat * _b)) -> abs(acci-1n), (f acci (acc,x))) xs (abs(List.length xs - 1n), acc) in res
    let try_remove (i:nat) (xs: _a list) : _a option * _a list = 
        try_item i xs, bindi (fun (j:nat) (x:_a) -> if i = j then [] else [x] ) xs
    let remove_unsafe (i:nat) (xs: _a list) : _a * _a list = 
        item_unsafe i xs, bindi (fun (j:nat) (x:_a) -> if i = j then [] else [x] ) xs
    let init (n:nat) (f: nat -> _a) : _a list = 
        let rec go ((m, xs): nat * _a list) : _a list =
            if m = 0n then xs else go (abs(m-1n), (f (abs(m-1n)))::xs) in
        go (n,([]:_a list)) 
    let repeat (n:nat) (f: _a -> _a) (x:_a) : _a = 
        let rec go ((m, x):nat * _a) : _a = 
            if m = n then x else f x in
        go (0n, x)
    // let try_remove (i:nat) (xs:_a list) : _a option * _a list = 
        // fold_right_i (fun (j:nat) ((x, (acc_mx, acc_xs)): _a * (_a option * _a list)) -> (if j = i then Some x, acc_xs else acc_mx, x::acc_xs)) xs ((None: _a option), ([]:_a list))
    let take_exact_rev_map (n:nat) (f:_a -> _b)(xs: _a list) : _b list * _a list =
        let rec go ((i, ys, xs): nat * _b list * _a list ) : _b list * _a list = 
            if i = n then ys, xs
            else match xs with x::xs -> go (i+1n, (f x)::ys, xs) | _ -> failwith "Too few - take_exact_rev" in
        go (0n, ([]:_b list), xs)
    let forall (fb: _a -> bool) (xs:_a list) : bool = List.fold_left (fun (acc, x : bool * _a) -> acc && fb x ) true xs
    let contains (eq: _a -> _a -> bool) (x:_a) (xs:_a list) : bool = List.fold_left (fun (acc, y : bool * _a) -> acc || eq x y) false xs
end

module BigMapHelpers = struct 
    let change (k:address*token_id) (fs: nat -> nat option) (ff: nat option) (m:((address*token_id), nat) big_map) = 
        Big_map.update k (match (Big_map.find_opt k m) with Some v -> fs v | _ -> ff) m 
end
module MapHelpers = struct 
    let change (k:address) (fs: nat -> nat option) (ff: nat option) (m:(address, nat) map) = 
        Map.update k (match (Map.find_opt k m) with Some v -> fs v | _ -> ff) m 
end
// let ok (x:_a) (s: _a -> _b) (_f: string -> _b) : _b = s x 
// let err (msg:string) (_s: _a -> _b) (f: string -> _b) : _b = f msg
// let bind 
//   (x: (_x -> ((_y -> _e) -> (string -> _e) -> _e) ) -> (string -> ((_y -> _e) -> (string -> _e) -> _e)) -> ((_y -> _e) -> (string -> _e) -> _e)) 
//   (fm: _a ->((_y -> _e) -> (string -> _e) -> _e) ) 
//   : (_y -> _e) -> (string -> _e) -> _e 
//   =
//   x (fun (okx:_x) -> fm okx) (fun (msg:string) (_s:_y -> _e) (f:string -> _e) -> f msg) 

// let x : (int -> _c) -> (string -> _c) -> _c = ok 3
// let y : (int -> _b) -> (string -> _b) -> _b = ok 4 
// let z : (int -> _b) -> (string -> _b) -> _b = err "invalid number" 

// let konst (a:_a) (_b:_b) : _a = a 

// let res (type b) () : (int -> b) -> (string -> b) -> b = 
//   bind (x: ((int -> (int -> b) -> (string -> b) -> b) -> (string -> (int -> b) -> (string -> b) -> b) -> (int -> b) -> (string -> b) -> b)) (fun (x:int) -> 
//   bind (y: ((int -> (int -> b) -> (string -> b) -> b) -> (string -> (int -> b) -> (string -> b) -> b) -> (int -> b) -> (string -> b) -> b)) (fun (y:int) ->
//   (ok: int -> (int -> b) -> (string -> b) -> b) 
//     (x * y)
//   ))

// let run_res : int = res () (identity: int -> int) (fun (msg:string) -> (failwith msg : int))

// type meta_sec = {counter:nat; pub:address; chain_id:chain_id}
// type meta = {meta_txn_data:bytes; sig:signature; }
// let verify_meta_txn (m:meta) : _a = 
//     let meta_sec, ep = match Bytes.unpack m.meta_txn_data : (meta_sec * _a) option with Some x -> x | _ -> "Failed to unpack meta txn." in 
//     if Crypto.check meta_sec.pub m.sig Crypto,blake2b(m.meta_txn_data) then ep else (failwith "Invalid signature." : _a)