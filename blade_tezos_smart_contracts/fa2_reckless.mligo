#include "fa2.mligo"
#include "helpers.mligo"

type minters = (address, token_id set) big_map
type propose_new_token_params = {token_id: token_id; token_info:bytes; initial_supply:(address * nat) list}
type approve_reject_new_token_params = {proposer: address; decision: bool} 
type token_proposals = (address, propose_new_token_params list) big_map 

type storage = {
  minters: minters;
  registrar: address;
  ledger: ledger;
  token_metadata: token_metadata_storage;
  token_proposals: token_proposals;
  metadata: contract_metadata;
  migration_status:migration_status;
  // delegations: (token_id, address option) big_map;
  //item_level: (token_id, nat) big_map;
  // powers: (token_id, nat) big_map;
//  user_metadata: (address, bytes) big_map;
}


let mint ((s, tds) : storage * transfer_destination list) =
  let new_ledger = 
    List.fold_left (fun ((acc, td):ledger * transfer_destination) ->
      let _ = match (Big_map.find_opt Tezos.sender s.minters : token_id set option) with | Some ts -> (if Set.mem td.token_id ts then () else failwith "No permission to mint this token") | _ -> failwith "No minting permission" in
      let _ = if Big_map.mem td.token_id s.token_metadata then () else failwith "This token is not registered so it cannot be minted." in
      Big_map.update 
        (td.to_, td.token_id) 
        (Some (
            match (Big_map.find_opt (td.to_, td.token_id) acc : nat option) with 
            | Some n ->  n + td.amount 
            | _ -> td.amount)
        ) acc) 
        s.ledger
        tds  in
  ([]:operation list), {s with ledger = new_ledger}

let approve_reject_new_tokens ((s, rs):storage * approve_reject_new_token_params) = 
  let _ = if Tezos.sender = s.registrar then () else failwith "You have no premission to approve/reject proposals." in
  let proposal = match Big_map.find_opt rs.proposer s.token_proposals with Some ts -> ts | _ -> (failwith "Proposal does not exist.": propose_new_token_params list) in  
  let new_proposals = Big_map.remove rs.proposer s.token_proposals in
  let new_metadatas_ledger = 
    List.fold_left (fun (((acc2, acc3), r):(token_metadata_storage * ledger) * propose_new_token_params) -> 
      (if rs.decision && Big_map.mem r.token_id acc2 then (failwith "Token already registered":token_metadata_storage) else (if rs.decision then Big_map.add r.token_id ({token_id=r.token_id; token_info=Map.literal[("",r.token_info)]}) acc2 else acc2)),
      (if rs.decision then List.fold_left (fun ((l,(a, n)):ledger * (address * nat)) -> Big_map.add (a, r.token_id) n l) acc3 r.initial_supply else acc3)
    ) (s.token_metadata, s.ledger) proposal in 
  ([]:operation list), {s with token_proposals = new_proposals; token_metadata = new_metadatas_ledger.0; ledger = new_metadatas_ledger.1}
let propose_new_tokens ((s,rs):storage * propose_new_token_params list) = 
  let _ = match Big_map.find_opt Tezos.sender s.token_proposals with Some _ -> failwith "You can make 1 proposal at a time." | _ -> () in 
  ([]:operation list), {s with token_proposals = Big_map.add Tezos.sender rs s.token_proposals}


type finalize_args = {seed:nat; offer_ids:nat list}
type fa2_entry_points =
  | Transfer of transfer list
  | Balance_of of balance_of_param
  | Update_operators of update_operator list
  | Mint of transfer_destination list
  | Propose_tokens of propose_new_token_params list
  | Approve_reject_token_proposal of approve_reject_new_token_params
  | Initialize of storage
  | Emigrate of address
  | Modify of storage -> storage
let main (action, s : fa2_entry_points * storage) : operation list * storage =
  let _ = 
    match action, s.migration_status with 
    | Initialize _, Waiting a -> if Tezos.sender = a then () else failwith "Invalid immigration address."
    | Emigrate _, Working -> if Tezos.sender = s.registrar then () else failwith "No permission to emigrate"
    | Initialize _, _ -> failwith "Already initialized"
    | Emigrate _, _ -> failwith "Either not initialized or already migrated"
    | _, Working -> ()
    | _, _ -> failwith "Either not initialized or migrated to new version"
    in
 match action with
 | Transfer t -> let _ = if can_transfer t then () else failwith "FA2_NOT_OWNER" in let (ops, l) = transfer (s.ledger, t) in ops, {s with ledger = l}
 | Mint m -> mint (s,m) 
 | Propose_tokens x -> propose_new_tokens (s,x)
 | Approve_reject_token_proposal x -> approve_reject_new_tokens (s,x)
 | Balance_of p -> [balance_of (s.ledger, p)], s
 | Initialize s -> ([]:operation list), s
 | Emigrate a -> (match (Tezos.get_entrypoint_opt "%initialize" a : storage contract option) with Some c -> [Tezos.transaction s 0tez c], {s with migration_status = Emigrated a} | None -> failwith "Invalid destination contract" : operation list * storage)
 | Modify f -> if Tezos.sender = s.registrar then ([]:operation list), f s else (failwith "No permission to modify contract.": operation list * storage)
 | _ -> (failwith "Not implemented":operation list * storage)


