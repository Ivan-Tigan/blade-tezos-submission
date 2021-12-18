set -x
admin_address="tz1XXqqcWUFXaBejQ3eGAS1Nevq77Y27Exov"
compiled=$(./ligo compile contract $1)
storage=$(./ligo compile storage $2 $3)
tezos-client originate contract $1 transferring 0 from $admin_address running "$compiled" --init "$storage" --burn-cap 2.59525 --force
