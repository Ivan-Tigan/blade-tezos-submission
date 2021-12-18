#include "fa2_reckless.mligo"
let initial_storage = {
  minters = (Big_map.literal [(("tz1Q3v9gjkG8gBHVDtG1aihop3dUVpEkvyUG":address),Set.literal[0n])]:minters);
  registrar = ("tz1Q3v9gjkG8gBHVDtG1aihop3dUVpEkvyUG":address);
  ledger = (Big_map.literal [
    ((("tz1Q3v9gjkG8gBHVDtG1aihop3dUVpEkvyUG":address), 0n), 1000n);
    ((("tz1YVUusb9uyHm58zsGi61EczuzvA2iMKy86":address), 0n), 1000n);
    ((("tz1WFmjWK4tetwFYBee16AeH4gqoEGkvTkfe":address), 0n), 1000n);
    ((("tz1iB6F57gdZczmAxctZDRHsjaEwDbzZTnYy":address), 0n), 1000n);
    ((("tz1abwEWbK7NZX1GKvsso9B3g3z4kRVSA9ri":address), 0n), 1000n);
    ((("tz1XXqqcWUFXaBejQ3eGAS1Nevq77Y27Exov":address), 0n), 1000n);
    

  ]: ledger);
  token_metadata = (Big_map.literal [
      (0n,
          {
            token_id=0n;
            token_info = 
                Map.literal 
                [
                    (("":string), 0x68747470733a2f2f736961736b792e6e65742f41414247472d2d4847725969615a422d314f64307835704e336f727a64724b393430303253504a58366864513677)
                ]
          }
        );
  ]:token_metadata_storage);
  token_proposals = (Big_map.empty: token_proposals);
  metadata = (Big_map.literal [(("":string),(0x68747470733a2f2f676973742e67697468756275736572636f6e74656e742e636f6d2f4976616e2d546967616e2f35633531316362646465313838333965666233363531356563613238346130612f7261772f393438323136663937626362343065636235646531366665333263393364336630303266336138372f5265636b6c6573732532353230436f6c6c656374696f6e25323532304d65746164617461: bytes))] :contract_metadata);
  migration_status = Working;
}

      // (1001n,
      //     {
      //       token_id=1001n;
      //       token_info = 
      //           Map.literal 
      //           [
      //               (("":string), 0x68747470733a2f2f676973742e67697468756275736572636f6e74656e742e636f6d2f4976616e2d546967616e2f32633038623766643734613764653465306266623035623135623836333731372f7261772f)
      //           ]
      //     }
      //   );
      // (1002n,
      //     {
      //       token_id=1002n;
      //       token_info = 
      //           Map.literal 
      //           [
      //               (("":string), 0x68747470733a2f2f676973742e67697468756275736572636f6e74656e742e636f6d2f4976616e2d546967616e2f63306461643437356330656135653133353432626238303237333932373361642f7261772f)
      //           ]
      //     }
      //   )
//KT1B2N9pZPN7EkgXt3dsnckZi6iVqpYnQZXb
//KT1CzXr7GmXLkCFpqNiumWar9FLAwQyUfNRM
//KT1HdznctmJqdKy7eob6D2tBjTrmTh3ThsC7
//KT1FHNqyn5mR4WcnjGzGz4SBZNZfnV43S8VU
//KT19PbwMNx5B1pC2zS1RATMjUCTnZZ1knGxz
//KT1HfHVRboGisJV5CZcfccZB9ykDMzBV529s
//KT1GAL7JTjXgkDwTpvWzbzgQ7P6jTcsDc1JZ
//KT1DBDnebEKfqXnVQhRYnuNiB9KK7oWXK4eL
//KT1AwmzMVKjMmXYTVpdQsDtneVcSTkE6JZKZ
//KT19uCDwxHpe3NV5hDZ8MyMGwcFTc6Jy1B7F
//KT1GJk9a4XPaqr5aRJynVWfcRUQFQrvuuFYU
//KT1UBuA4EzVvYnc67NbJif4iQ3kiQ6GDN6ME
//KT1JHovuB8y56uMNLt7M5pZonUTNmH7kYuzu
//KT1FPENN4459ZiFjkaT1anpektDN16os6hK3
//KT1L8WURwm37hWFhrZefDd9tboJowdkQv1LB
//KT1Mw7E46UuQk62imBoYzTSUCpuz3LLXZ7qo

let mk_storage (registrar:address) (zero_md:bytes) (md:bytes) = {
  minters = (Big_map.literal [((registrar),Set.literal[0n])]:minters);
  registrar = registrar;
  ledger = (Big_map.literal [
    ((("tz1Q3v9gjkG8gBHVDtG1aihop3dUVpEkvyUG":address), 0n), 1000n);
    ((("tz1YVUusb9uyHm58zsGi61EczuzvA2iMKy86":address), 0n), 1000n);
    ((("tz1WFmjWK4tetwFYBee16AeH4gqoEGkvTkfe":address), 0n), 1000n);
    ((("tz1iB6F57gdZczmAxctZDRHsjaEwDbzZTnYy":address), 0n), 1000n);
    ((("tz1abwEWbK7NZX1GKvsso9B3g3z4kRVSA9ri":address), 0n), 1000n);
    ((("tz1XXqqcWUFXaBejQ3eGAS1Nevq77Y27Exov":address), 0n), 1000n);
    

  ]: ledger);
  token_metadata = (Big_map.literal [
      (0n,
          {
            token_id=0n;
            token_info = 
                Map.literal 
                [
                    (("":string), zero_md)
                ]
          }
        );
  ]:token_metadata_storage);
  token_proposals = (Big_map.empty: token_proposals);
  metadata = (Big_map.literal [(("":string),(md: bytes))] :contract_metadata);
  migration_status = Working;
  
}

let prison = 
  {
    minters = (Big_map.empty:minters);
    registrar = ("tz1L5VPHtKEfHYVBcv9762mnK4HzqBucxoim":address);
    ledger = (Big_map.empty : ledger);
    token_metadata = (Big_map.empty:token_metadata_storage );
    token_proposals = (Big_map.empty: token_proposals);
    metadata = (Big_map.literal [(("":string),(0x68747470733a2f2f676973742e67697468756275736572636f6e74656e742e636f6d2f5374616e417274732f39333432653262366334663134313836656166336264646632613332626566302f7261772f: bytes))] :contract_metadata);
    migration_status = Working;
  }
let blade = 
  mk_storage ("tz1XXqqcWUFXaBejQ3eGAS1Nevq77Y27Exov":address)
    (0x68747470733a2f2f676973742e67697468756275736572636f6e74656e742e636f6d2f4976616e2d546967616e2f33353831326135656131383636336364346462343164343863666266373364642f7261772f:bytes)
    (0x68747470733a2f2f676973742e67697468756275736572636f6e74656e742e636f6d2f4976616e2d546967616e2f66383634333530326435313538323465376334313938333365303636383639302f7261772f:bytes)
//https://gist.githubusercontent.com/StanArts/9342e2b6c4f14186eaf3bddf2a32bef0/raw/