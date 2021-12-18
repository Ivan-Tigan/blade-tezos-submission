#if ! FA2_INTERFACE
#define FA2_INTERFACE
type fa12_transfer = [@layout:comb] { from : address; [@annot:to]to_: address; value: nat; }
type token_id = nat
type transfer_destination = [@layout:comb] { to_ : address; token_id : token_id; amount : nat; }
type transfer = [@layout:comb] { from_ : address; txs : transfer_destination list; }
type balance_of_request = [@layout:comb] { owner : address; token_id : token_id; }
type balance_of_response = [@layout:comb] { request : balance_of_request; balance : nat; }
type balance_of_param = [@layout:comb] { requests : balance_of_request list; callback : (balance_of_response list) contract; }
type operator_param = [@layout:comb] { owner : address; operator : address; token_id: token_id; }
type update_operator = [@layout:comb] | Add_operator of operator_param | Remove_operator of operator_param
type token_metadata = [@layout:comb] { token_id : token_id; token_info : (string, bytes) map; }

type token_metadata_storage = (token_id, token_metadata) big_map
type token_metadata_param = [@layout:comb] { token_ids : token_id list; handler : (token_metadata list) -> unit; }
type contract_metadata = (string, bytes) big_map

(* FA2 hooks interface *)

type transfer_destination_descriptor = [@layout:comb] { to_ : address option; token_id : token_id; amount : nat; }

type transfer_descriptor = [@layout:comb] { from_ : address option; txs : transfer_destination_descriptor list }

type transfer_descriptor_param = [@layout:comb] { batch : transfer_descriptor list; operator : address; }

(*
Entrypoints for sender/receiver hooks

type fa2_token_receiver =
  ...
  | Tokens_received of transfer_descriptor_param

type fa2_token_sender =
  ...
  | Tokens_sent of transfer_descriptor_param
*)

#endif


type ledger = ((address * nat), nat) big_map
let balance_of ((l, p) : ledger * balance_of_param) : operation = 
  let res = List.map (fun (breq:balance_of_request) -> {request=breq; balance= match Big_map.find_opt (breq.owner, breq.token_id) l with Some n -> n | _ -> 0n } ) p.requests in 
  Tezos.transaction res 0tez p.callback 

let can_transfer (ts:transfer list) = Tezos.sender = Tezos.self_address || List.fold_left (fun ((b, t): bool * transfer) -> b && (Tezos.source = t.from_ || Tezos.sender = t.from_)) true ts
let transfer ((ledger, ts):ledger * transfer list) = 
  let new_ledger = 
    List.fold_left (fun ((l, t):(ledger * transfer)) -> 
      List.fold_left (fun ((l, r):ledger * transfer_destination) -> 
        match (Big_map.find_opt (t.from_, r.token_id) l, Big_map.find_opt (r.to_, r.token_id) l : (nat option * nat option)) with
        | Some n1, Some n2 -> 
          let _ = if r.amount > n1 then failwith "FA_INSUFFICIENT_BALANCE" else () in 
          let remove = Big_map.update (t.from_, r.token_id) (Some (abs(n1 - r.amount))) l in
          let add = Big_map.update (r.to_, r.token_id) (Some (r.amount + n2)) remove in
          add
        | Some n1, None -> 
          let _ = if r.amount > n1 then failwith "FA2_INSUFFICIENT_BALANCE" else () in 
          let remove = Big_map.update (t.from_, r.token_id) (Some (abs(n1 - r.amount))) l in
          let add = Big_map.update (r.to_, r.token_id) (Some (r.amount)) remove in
          add
        | None, _ -> (failwith "FA2_INSUFFICIENT_BALANCE" : ledger)
      ) l t.txs) ledger ts in
  ([]:operation list), new_ledger

let mk_fa12_transfer_op (a : address) (fa12tr: fa12_transfer) = match (Tezos.get_entrypoint_opt "%transfer" a : fa12_transfer contract option) with Some c -> Tezos.transaction fa12tr 0tez c | _ -> (failwith "Invalid fa1.2 contract" : operation)
// let mk_fa12_transfer_ops (fa12trs:(address * fa12_transfer) list) = List.map mk_fa12_transfer_op fa12trs
let mk_fa2_transfer_op (a:address) (fa2tr: transfer list) = match (Tezos.get_entrypoint_opt "%transfer" a : (transfer list) contract option) with Some c -> Tezos.transaction fa2tr 0tez c | _ -> (failwith "Invalid fa2 contract" : operation)
// let mk_fa2_transfer_ops (fa2trs:(address * transfer list) list) = List.map mk_fa2_transfer_op fa2trs
// let mk_transfer_ops ((f12, f2):(address * fa12_transfer) list * (address * transfer list) list) = List.fold_left (fun (acc, f2) -> (mk_fa2_transfer_op f2)::acc) (List.map mk_fa12_transfer_op f12) f2

type fees = (address, nat * (address , nat) map) big_map 

let apply_fees_fa2 ((a, ts, fees) : address * transfer list * fees) = 
  let (rem, fs) = match Map.find_opt a fees with Some x -> x | _ -> 100n, (Map.empty : (address, nat) map) in
  let fees_total = Map.fold (fun ((acc, (_,n)) : nat * (address * nat)) -> acc + n) fs rem in
  List.map (fun (tr:transfer) -> 
    { tr with 
    txs = 
      List.fold_left (fun ((acc, trd) : transfer_destination list * transfer_destination) -> 
        let (total_transfer, trds) = 
          Map.fold (fun (((accn, acc), (a,w): (nat * transfer_destination list) * (address * nat)) ) -> let n = (trd.amount * w)/fees_total in accn+n, {to_ = a; token_id = trd.token_id; amount = n}::acc)
          (Map.add trd.to_ (match Map.find_opt trd.to_ fs with Some n -> n + rem | _ -> rem) fs)
          (0n, acc) in 
        {trd with amount = if trd.amount >= total_transfer then abs(trd.amount - total_transfer) else (failwith "critical error" : nat)} :: trds 
        ) 
      ([]:transfer_destination list) 
      tr.txs 
    })
    ts
let mk_tez_transfer_op (to_:address) (tz:tez) : operation = 
    Tezos.transaction () tz (match (Tezos.get_contract_opt to_ : unit contract option) with Some a -> a | _ ->(failwith "Invalid recipient for tez":unit contract))
let mk_tez_transfer_opf (to_:address) (tz:tez) : (address * tez) = 
    to_, tz

type rev_share = (address, nat) map
let total_shares (shares:rev_share) = Map.fold (fun (acc, (_a, n) : nat * (address * nat)) -> acc + n) shares 0n

let apply_rev_share_tez (amnt, shares: tez * (address, nat) map) =
  let total = total_shares shares in
  let (remaddr, total_transfered, transfers) = Map.fold (fun ((_remaddr, total_transfered, ops), (a, n): (address option * tez * operation list) * (address * nat)) -> let t = ((n*amnt)/total) in Some a, total_transfered + t, mk_tez_transfer_op a t :: ops ) shares ((None:address option), 0tez, ([] : operation list)) in 
  if amnt > total_transfered then (match remaddr with Some a -> mk_tez_transfer_op a (amnt - total_transfered) :: transfers | _ -> transfers) else transfers

let apply_rev_share_fa12 (fa12_address, tr, shares : address * fa12_transfer * rev_share) = 
  let total = total_shares shares in
  let (remaddr, total_transfered, transfers) = Map.fold (fun ((_remaddr, total_transfered, ops), (a, n): (address option * nat * operation list) * (address * nat)) -> let t = ((n*tr.value)/total) in Some a, total_transfered + t, mk_fa12_transfer_op fa12_address {tr with to_=a; value=t} :: ops ) shares ((None:address option), 0n, ([] : operation list)) in 
  if tr.value > total_transfered then (match remaddr with Some a -> mk_fa12_transfer_op fa12_address {tr with to_=a; value=abs(tr.value - total_transfered)} :: transfers | _ -> transfers) else transfers

let apply_rev_share_fa2 (tr, shares :transfer_destination * rev_share) = 
  let total = total_shares shares in
  let (remaddr, total_transfered, transfers) = Map.fold (fun ((_remaddr, total_transfered, ops), (a, n): (address option * nat * transfer_destination list) * (address * nat)) -> let t = ((n*tr.amount)/total) in Some a, total_transfered + t, {to_=a; amount=t; token_id=tr.token_id} :: ops ) shares ((None:address option), 0n, ([] : transfer_destination list)) in 
  if tr.amount > total_transfered then (match remaddr with Some a -> {to_=a; amount=abs(tr.amount - total_transfered); token_id=tr.token_id} :: transfers | _ -> transfers) else transfers


let apply_fees_fa12 ((a, t, fees) : address * fa12_transfer * fees) =
  let (rem, fs) = match Map.find_opt a fees with Some x -> x | _ -> 100n, (Map.empty : (address, nat) map) in
  let fees_total = Map.fold (fun ((acc, (_,n)) : nat * (address * nat)) -> acc + n) fs rem in
  let (total_transfer, trds) = 
    Map.fold (fun (((accn, acc), (a,w): (nat * fa12_transfer list) * (address * nat)) ) -> let n = (t.value * w)/fees_total in accn+n, { t with to_ = a; value = n}::acc)
    (Map.add t.to_ (match Map.find_opt t.to_ fs with Some n -> n + rem | _ -> rem) fs)
    (0n, ([]:fa12_transfer list)) in 
  {t with value = if t.value >= total_transfer then abs(t.value - total_transfer) else (failwith "critical error" : nat)} :: trds 

// type revenue_share = (address, nat) map
// let fa12_rev_share (rs, from, token_address, n : revenue_share * address * address * nat) = 
//   let total_shares = Map.fold (fun (acc, (k,v) : nat * (address * nat)) -> acc + v) rs 0n in 
//   let txs = Map.fold (fun (acc, (k,v) : operation list * (address * nat)) -> mk_fa12_transfer_op token_address {from=from; to_=k; value= } )

type token_type = Fa12 of address | Fa2 of address * token_id | Tez of bool