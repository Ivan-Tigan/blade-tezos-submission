type migration_status = Waiting of address | Working | Emigrated of address
type storage = {data:int; migration_status:migration_status}

type parameter =
  Increment of int
| Decrement of int
| Initialize of storage
| Emigrate of address

type return = operation list * storage

// Two entrypoints

let add (store, delta : storage * int) : storage = {store with data = store.data + delta}
let sub (store, delta : storage * int) : storage = {store with data = store.data - delta}

(* Main access point that dispatches to the entrypoints according to
   the smart contract parameter. *)
let noop = ([] : operation list)
let main (action, store : parameter * storage) : return =
 let _ = 
  match action, store.migration_status with 
  | Initialize _, Waiting a -> if Tezos.sender = a then () else failwith "Invalid immigration address."
  | Emigrate _, Working -> if Tezos.sender = Tezos.sender then () else failwith "No permission to emigrate"
  | Initialize _, _ -> failwith "Already initialized"
  | Emigrate _, _ -> failwith "Either not initialized or already migrated"
  | _, Working -> ()
  | _, _ -> failwith "Either not initialized or migrated to new version"
  in
     // No operations
 (match action with
   Increment (n) -> noop, add (store, n)
 | Decrement (n) -> noop, sub (store, n)
 | Initialize s -> noop, s
 | Emigrate a -> (match (Tezos.get_entrypoint_opt "%initialize" a : storage contract option) with Some c -> [Tezos.transaction store 0tez c], {store with migration_status = Emigrated a} | None -> failwith "Invalid destination contract" : return)
 )
